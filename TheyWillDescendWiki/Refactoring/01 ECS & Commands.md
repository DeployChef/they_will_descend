# 01. Реформа ECS и команд

← [[Index|План рефакторинга]] | Далее → [[02 Baking, SubScenes & World Bootstrap|02 Бейкинг и саб-сцены]]

Документ описывает устранение командного синглтона `SimSession` и переход на две профессиональные модели взаимодействия: **Reconciliation Loop** (для непрерывного состояния) и **Request Entities** (для разовых триггеров).

---

## 1. Ликвидация «Брокера команд» на `SimSession`

### Как сделано сейчас (Антипаттерн):
```text
UI клик 
  → SimCommands.TryPost (добавление в DynamicBuffer<T> на синглтоне)
  → следующий тик: ConsumeXCommandsSystem
  → линейный перебор всех сущностей через ToComponentDataArray для поиска по int Id
  → применение
```
**Минусы:**
* 20+ буферов на одной сущности.
* Задержка в 1 кадр на любое действие.
* Квадратичные сканы $O(N)$ и массовые аллокации в памяти.

### Как будет:
Шина буферов на `SimSession` **полностью удаляется**.
Взаимодействие разделяется на два чистых потока:
1. **Регуляторы и штат $\rightarrow$ Вариант А (Desired State).**
2. **Разовые действия $\rightarrow$ Вариант Б (Transient Request Entities).**

---

## 2. Вариант А: Reconciliation Loop (Назначение рабочих и параметры)

### Идея
Игрок не дает приказов отдельным жителям. Игрок выставляет **целевое состояние объекта** (`Desired State`). Фоновая система симуляции непрерывно выравнивает реальность под желание игрока.

### Модель данных
```csharp
namespace TheyWillDescend.Simulation.City
{
    public struct Workplace : IComponentData
    {
        public int MaxSlots;        // Максимум рабочих (из каталога/типа)
        public int DesiredWorkers;  // Желаемое число рабочих (выставил игрок в UI)
        public int AssignedCount;   // Фактически назначено (идут или работают)
        public int WorkingCount;    // Физически дошли до здания и работают
        public bool IsPaused;       // Здание отключено игроком
    }
}
```

### UI (BuildingInspectPanel)
UI знает прямую `Entity` выбранного дома:
```csharp
// Кнопка "+" в интерфейсе:
public void OnPlusWorker()
{
    if (_selectedEntity == Entity.Null) return;
    
    var wp = _entityManager.GetComponentData<Workplace>(_selectedEntity);
    if (wp.DesiredWorkers < wp.MaxSlots)
    {
        wp.DesiredWorkers++;
        _entityManager.SetComponentData(_selectedEntity, wp);
    }
}

// Кнопка "Пауза":
public void OnTogglePause()
{
    if (_selectedEntity == Entity.Null) return;
    
    var wp = _entityManager.GetComponentData<Workplace>(_selectedEntity);
    wp.IsPaused = !wp.IsPaused;
    _entityManager.SetComponentData(_selectedEntity, wp);
}
```
**Результат в UI:** 2 строчки кода, отклик за 0 мс, ноль команд.

### Фоновая диспетчеризация (`WorkforceDispatchSystem`)
Чистая Burst-система:
```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(AdvanceAgentCommuteSystem))]
public partial struct WorkforceDispatchSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 1. Дефицит рабочих: DesiredWorkers > AssignedCount (при !IsPaused)
        //    Берем ближайших свободных жителей (AgentAssignment.Workplace == Entity.Null)
        //    и направляем их к этому зданию, увеличивая AssignedCount.
        
        // 2. Избыток рабочих: DesiredWorkers < AssignedCount (или IsPaused == true)
        //    Отзываем лишних рабочих на Плазу, уменьшая AssignedCount.
    }
}
```

### Почему это ключевое улучшение:
* **Самовосстановление (Self-healing):** Если рабочего в пути убило знамением или он умер от голода, система в следующем тике видит: `AssignedCount < DesiredWorkers` и **сама отправляет замену** из свободных! При командной модели пришлось бы городить сложнейшие откаты и отслеживание статусов.

---

## 3. Вариант Б: Transient Request Entities (Стройка, снос, ритуалы)

### Идея
Для разовых действий, у которых нет своего постоянного компонента, UI создает **временную сущность-запрос**. Она существует ровно один тик и уничтожается в конце кадра через `EntityCommandBuffer` (ECB).

### Пример: Размещение здания (`PlaceBuildingRequest`)
```csharp
// Компонент запроса (отдельный чистый struct):
public struct PlaceBuildingRequest : IComponentData
{
    public FixedString64Bytes TypeId;
    public int AnchorCluster;
    public int AnchorRadial;
    public int WidthClusters;
    public int DepthRadialRings;
}

// Отправка из BuildPlacementController:
void Place()
{
    var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                       .CreateCommandBuffer(World.DefaultGameObjectInjectionWorld.Unmanaged);
    var req = ecb.CreateEntity();
    ecb.AddComponent(req, new PlaceBuildingRequest
    {
        TypeId = _typeId,
        AnchorCluster = _anchorCluster,
        AnchorRadial = _anchorRadial,
        WidthClusters = _footprint.WidthClusters,
        DepthRadialRings = _footprint.DepthRadialRings
    });
}
```

### Обработка в симуляции (`ProcessPlaceBuildingRequestsSystem`)
```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial struct ProcessPlaceBuildingRequestsSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (req, entity) in SystemAPI.Query<RefRO<PlaceBuildingRequest>>().WithEntityAccess())
        {
            // Валидация клетки, вычитание ресурсов, создание сущности здания
            // ...
            // В конце кадра запрос уничтожается:
            ecb.DestroyEntity(entity);
        }
    }
}
```

---

## 4. Прямые ссылки `Entity` вместо `int Id`

### Было:
* В компонентах: `int WorkplaceBuildingId`.
* В поиске: `for (int i=0; i<buildings.Length; i++) if (b[i].Id == id)`.
* Временные массивы `ToComponentDataArray` каждый кадр.

### Стало:
* В компонентах: `Entity WorkplaceBuilding`.
* Доступ: `SystemAPI.GetComponent<Workplace>(assignment.WorkplaceBuilding)` — мгновенно за $O(1)$.
* `int BuildingId` сохраняется **только в DTO при Save/Load** (для файла сохранения), но в рантайме не используется для поиска.

---

## 5. Что удаляется в результате реформы

* ❌ `AssignWorkerCommand.cs`
* ❌ `UnassignWorkerCommand.cs`
* ❌ `SetWorkplacePausedCommand.cs`
* ❌ `SetPyramidFeedCommand.cs`
* ❌ `ConsumeAssignWorkerCommandsSystem.cs`
* ❌ `ConsumeUnassignWorkerCommandsSystem.cs`
* ❌ `ConsumeSetWorkplacePausedCommandsSystem.cs`
* ❌ `ConsumeSetPyramidFeedCommandsSystem.cs`
* ❌ Все соответствующие буферы на `SimSession`.

Кодовая база уменьшается на сотни строк бойлерплейта, а симуляция становится быстрой и понятной.
