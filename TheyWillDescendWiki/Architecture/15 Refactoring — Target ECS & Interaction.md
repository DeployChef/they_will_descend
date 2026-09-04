# 15 Refactoring — Target ECS & Interaction

← [[14 Sim Presentation Bridge|14 Bridge]] | [[Index]] | [[../Home|Home]]

Архитектурный гайд по рефакторингу подсистемы взаимодействия игрока и симуляции (UI ↔ ECS). Переход от промежуточного «SQL-брокера команд на синглтоне» к каноничной production-модели Unity DOTS.

---

## 1. Проблема текущей реализации (Диагноз)

В текущем вертикальном срезе симуляция работает через централизованный брокер команд:
1. **Синглтон-монстр `SimSession`:** Хранит более 20 `DynamicBuffer<TCommand>`, превращаясь в перегруженную шину сообщений. Добавление любой механики требует правок в `SimSessionAccess`, Authoring, Baker и жизненном цикле.
2. **Искусственные `int Id` и линейные поиски $O(N)$:** Вместо ссылок на `Entity` везде используются целочисленные идентификаторы (`Building.Id`, `AgentId.Value`). В результате в системах консьюмеров ([`ConsumeAssignWorkerCommandsSystem`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Simulation/Agents/CommandSystems/ConsumeAssignWorkerCommandsSystem.cs) и [`ConsumeUnassignWorkerCommandsSystem`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Simulation/Agents/CommandSystems/ConsumeUnassignWorkerCommandsSystem.cs)) и в UI-инспекторах каждый кадр создаются динамические `EntityQuery` и временные массивы `ToComponentDataArray` для линейного поиска объектов в циклах `for`.
3. **Квадратичные накладные расходы:** Например, метод `UnassignAll(buildingId)` в цикле `while` вызывает `UnassignOne`, который на каждого отдельного рабочего создает два новых `EntityQuery`, аллоцирует NativeArray и ищет цель перебором с начала массива.
4. **Бойлерплейт-паралич:** Чтобы поддержать простое действие, требуется создать команду, буфер на синглтоне, отдельную `ConsumeXCommandsSystem` и обвязку в Presentation.

---

## 2. Целевая архитектура: Разделение по природе взаимодействий

В профессиональном продакшене взаимодействия игрока делятся на **два фундаментальных типа**:

```text
                                ИГРОК / UI
                                     │
         ┌───────────────────────────┴───────────────────────────┐
         │                                                       │
         ▼                                                       ▼
   ТИП 1: РЕГУЛЯТОРЫ И ШТАТ                        ТИП 2: ДИСКРЕТНЫЕ ДЕЙСТВИЯ
  (Рабочие, приоритеты, пауза,                     (Поставить здание, снести,
      слайдеры дани HQ)                               ритуал, закон)
         │                                                       │
         ▼                                                       ▼
     ВАРИАНТ А                                               ВАРИАНТ Б
 Декларативное состояние                               Временные сущности-запросы
  (Reconciliation Loop)                               (Transient Request Entities)
         │                                                       │
         ▼                                                       ▼
Прямая запись целевых полей                         Создание Request Entity
в Entity выбранного здания                          через EntityCommandBuffer (ECB)
         │                                                       │
         ▼                                                       ▼
Фоновая ISystem на Burst                            Фоновая ISystem на Burst
выравнивает факт под желание                        обрабатывает запрос и удаляет его
(Self-healing логика)                               в том же кадре
```

---

## 3. Вариант А: Декларативное целевое состояние (Reconciliation Loop)

**Где применяется:** Назначение рабочих, пауза производства, приоритеты зданий, слайдеры сжигания дани на пирамиде.

### Принцип работы
Игрок в UI не должен управлять отдельными транзакциями («найди мне рабочего прямо сейчас»). Игрок задает **желаемое состояние (Desired State)**. Симуляция непрерывно приводит фактическое состояние к желаемому.

### Модель данных (ECS Component)
Вместо счетчика назначенных рабочих на здании хранится целевой штат:

```csharp
namespace TheyWillDescend.Simulation.City
{
    public struct Workplace : IComponentData
    {
        public int MaxSlots;        // Лимит рабочих (из каталога/типа)
        public int DesiredWorkers;  // Сколько хочет видеть игрок (0..MaxSlots)
        public int AssignedCount;   // Сколько рабочих фактически привязано
        public int WorkingCount;    // Сколько рабочих физически дошли до здания
        public bool IsPaused;       // Ручная остановка производства
    }
}
```

### UI Interaction (BuildingInspectPanel)
UI знает конкретную выбранную `Entity` здания (полученную из Raycast/Selection). Обращение происходит напрямую за $O(1)$:

```csharp
// Клик на кнопку "+" в инспекторе
public void OnPlusWorker()
{
    if (_selectedEntity == Entity.Null || !_entityManager.Exists(_selectedEntity))
        return;

    var workplace = _entityManager.GetComponentData<Workplace>(_selectedEntity);
    if (workplace.DesiredWorkers < workplace.MaxSlots)
    {
        workplace.DesiredWorkers++;
        _entityManager.SetComponentData(_selectedEntity, workplace);
    }
}

// Клик на кнопку "Пауза / Питание"
public void OnTogglePower()
{
    if (_selectedEntity == Entity.Null || !_entityManager.Exists(_selectedEntity))
        return;

    var workplace = _entityManager.GetComponentData<Workplace>(_selectedEntity);
    workplace.IsPaused = !workplace.IsPaused;
    _entityManager.SetComponentData(_selectedEntity, workplace);
}
```

### Симуляция (`WorkforceDispatchSystem`)
Чистая, параллельная система на Burst без единой команды:

```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(AdvanceAgentCommuteSystem))]
public partial struct WorkforceDispatchSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 1. Поиск зданий с дефицитом: DesiredWorkers > AssignedCount (при !IsPaused)
        //    Берем свободных жителей (AgentAssignment.Workplace == Entity.Null) 
        //    и назначаем на здание, инкрементируя AssignedCount.
        
        // 2. Поиск зданий с избытком: DesiredWorkers < AssignedCount (или IsPaused == true)
        //    Отзываем лишних жителей (сбрасываем цель на Плазу) 
        //    и декрементируем AssignedCount.
    }
}
```

### Почему это победа:
* **Самовосстановление (Self-healing):** Если рабочего в пути убило метеоритом или он умер от голода, система на следующем тике видит: `AssignedCount < DesiredWorkers` и **автоматически** отправляет на замену нового жителя. Никаких rollback-команд писать не нужно!
* **Удаление классов:** Полностью удаляются `AssignWorkerCommand`, `UnassignWorkerCommand`, `SetWorkplacePausedCommand`, `ConsumeAssignWorkerCommandsSystem`, `ConsumeUnassignWorkerCommandsSystem`, `ConsumeSetWorkplacePausedCommandsSystem`.
* **Быстродействие:** Ноль аллокаций, ноль переборов по `int Id`.

---

## 4. Вариант Б: Временные сущности-запросы (Transient Request Entities)

**Где применяется:** Дискретные разовые действия, у которых нет постоянного состояния: постройка нового здания, принудительный снос, запуск разового ритуала на пирамиде, принятие эдикта/закона.

### Принцип работы
1. В момент клика UI создает временную `Entity` с компонентом-запросом через `EntityCommandBuffer` (ECB).
2. Запрос живет ровно **один тик**.
3. Соответствующая система симуляции считывает запрос, выполняет валидацию/логику и в конце кадра уничтожает сущность-запрос через ECB.

### Пример: Размещение здания (`PlaceBuildingRequest`)

#### Структура запроса:
```csharp
namespace TheyWillDescend.Simulation.City
{
    public struct PlaceBuildingRequest : IComponentData
    {
        public FixedString64Bytes TypeId;
        public int AnchorCluster;
        public int AnchorRadial;
        public int WidthClusters;
        public int DepthRadialRings;
    }
}
```

#### Отправка из UI (`BuildPlacementController`):
```csharp
void PlaceBuilding()
{
    var world = World.DefaultGameObjectInjectionWorld;
    var ecb = world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
                   .CreateCommandBuffer();

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

#### Обработка в симуляции (`ProcessPlaceBuildingRequestsSystem`):
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

        foreach (var (req, entity) in 
                 SystemAPI.Query<RefRO<PlaceBuildingRequest>>().WithEntityAccess())
        {
            // 1. Проверяем валидность размещения и ресурсы
            // 2. Создаем Entity здания и проставляем компоненты
            // 3. Уничтожаем временную сущность запроса
            ecb.DestroyEntity(entity);
        }
    }
}
```

### Пример: Снос здания (`DemolishBuildingRequest`)
UI передает прямую `Entity` целевого здания, а не целочисленный id:
```csharp
public struct DemolishBuildingRequest : IComponentData
{
    public Entity TargetBuilding;
}
```
Система мгновенно обращается к `req.TargetBuilding` за $O(1)$ без поиска в массивах.

---

## 5. Доступ по `Entity` вместо `int Id`

### Как сосуществуют Save/Load и Runtime
* **В рантайме:** Связи строятся через **`Entity`** (например, `AgentAssignment.WorkplaceBuilding` имеет тип `Entity`, а не `int`). Все выборки, проверки компонентов и команды оперируют `Entity`.
* **В сохранении (Save/Load):** `Entity(53, 1)` нельзя сохранять в JSON, так как после перезапуска Unity индексы сущностей меняются.
* **Решение:** Компонент `BuildingId { public int Value; }` остается на сущности **исключительно как сериализуемый ключ**. 
  * При сохранении `RunSessionSnapshot` считывает `BuildingId`.
  * При загрузке восстанавливает `Entity` и маппит связи через локальный `NativeParallelHashMap<int, Entity>`.
  * В обычном геймплее поиск по `BuildingId` **запрещен**.

---

## 6. План рефакторинга (Migration Roadmap)

### Фаза 1. Рефакторинг рабочих (Переход на Вариант А)
1. Расширить `Workplace` полями `DesiredWorkers` и `IsPaused`.
2. Написать `WorkforceDispatchSystem` (выравнивание `AssignedCount` к `DesiredWorkers`).
3. В `AgentAssignment` заменить `int WorkplaceBuildingId` на `Entity WorkplaceBuilding`.
4. Переписать кнопки `+`, `-`, `Max`, `Power` в [`BuildingInspectPanel.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Presentation/GameHud/BuildingInspectPanel.cs) на прямое изменение `Workplace`.
5. Удалить:
   - `AssignWorkerCommand.cs`, `UnassignWorkerCommand.cs`, `SetWorkplacePausedCommand.cs`
   - `ConsumeAssignWorkerCommandsSystem.cs`, `ConsumeUnassignWorkerCommandsSystem.cs`, `ConsumeSetWorkplacePausedCommandsSystem.cs`
   - Очистить соответствующие буферы на `SimSession`.

### Фаза 2. Рефакторинг подачи дани на Пирамиде (Вариант А)
1. Слайдеры в [`PyramidInspectPanel.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Presentation/GameHud/PyramidInspectPanel.cs) пишут желаемый расход напрямую в буфер `PyramidFeedLine` на сущности `Headquarters`.
2. Удалить `SetPyramidFeedCommand` и `ConsumeSetPyramidFeedCommandsSystem`.

### Фаза 3. Рефакторинг стройки и сноса (Переход на Вариант Б)
1. Создать компоненты `PlaceBuildingRequest` и `DemolishBuildingRequest`.
2. Переписать `BuildPlacementController` и кнопку сноса на спавн Request Entities через ECB.
3. Системы `ProcessPlaceBuildingRequestsSystem` и `ProcessDemolishBuildingRequestsSystem` заменяют старые консьюмеры.
4. Удалить `PlaceBuildingCommand`, `DeconstructBuildingCommand` и соответствующие буферы на `SimSession`.

### Фаза 4. Оптимизация Presentation (`ViewBoards` & UI Panels)
1. В [`AgentViewBoard.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Presentation/Agents/AgentViewBoard.cs) и [`BuildingViewBoard.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Presentation/City/BuildingViewBoard.cs) заменить вызовы `ToComponentDataArray` на прямую итерацию сущностей без аллокаций.
2. Заменить `DestroyImmediate` на безопасный `Destroy` (или пулинг).
3. Кэшировать выбранную `Entity` в `BuildingInspectPanel`, убрать строковые конкатенации из `Update()`.

---

## 7. Сравнительная таблица (До и После)

| Метрика / Аспект | До рефакторинга (Текущее состояние) | После рефакторинга (Целевое состояние) |
| :--- | :--- | :--- |
| **Шина команд** | 20+ `DynamicBuffer` на синглтоне `SimSession` | Нет центрального брокера; Request Entities через ECB |
| **Назначение рабочих** | Команды $\rightarrow$ Очередь $\rightarrow$ Поиск $O(N)$ $\rightarrow$ Привязка | Декларативный `DesiredWorkers` $\rightarrow$ Самовыравнивание |
| **Сложность поиска** | $O(N)$ перебор по `int Id` с созданием `EntityQuery` | $O(1)$ прямой доступ по `Entity` |
| **Реакция на гибель рабочего** | Ручная отмена команды / рассинхрон | Система сама берет замену в следующем кадре |
| **Аллокации в кадре** | Десятки временных `NativeArray` в Presentation и консьюмерах | 0 байт GC и 0 Temp Allocations в стабильном кадре |
| **Добавление новой механики** | Создать 6–8 файлов (команда, буфер, консьюмер, DTO...) | 1 Request Component или 1 поле на целевом компоненте |

---

Связанные разделы: [[03 Core Systems]] · [[04 Simulation]] · [[08 Production ECS]] · [[14 Sim Presentation Bridge]]
