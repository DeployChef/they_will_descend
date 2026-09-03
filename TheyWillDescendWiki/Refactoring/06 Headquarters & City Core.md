# 06. Пирамида и Сердце Города (Headquarters & City Core)

← [[05 Content Pipeline & CMS|05 Контент и CMS]] | [[Index|План рефакторинга]] | [[../Home|Главная вики]]

Исчерпывающий архитектурный разбор и гайд по рефакторингу главного здания игры — Великой Пирамиды майя (`Headquarters`). Устранение паразитных систем, циклических костылей и интеграция в чистый `SimulationBootstrap`.

---

## 1. Анатомия проблемы: Полный диагноз по кодовой базе

В текущей кодовой базе главное здание страдает от целого комплекса архитектурных аномалий.

### 1.1. Параноидальная синхронизация неподвижного объекта
* **В саб-сцене:** Запекальщик [`HeadquartersCenterBakeSystem.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Authoring/City/HeadquartersCenterBakeSystem.cs#L21) находит трансформ Пирамиды и записывает его в `CityGrid.Center`.
* **В рантайме:** Поверх этого работает система [`SyncCityCenterFromHeadquartersSystem.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Simulation/City/Systems/SyncCityCenterFromHeadquartersSystem.cs#L20-L29):
  ```csharp
  [UpdateInGroup(typeof(SimulationSystemGroup))]
  public partial struct SyncCityCenterFromHeadquartersSystem : ISystem
  {
      public void OnUpdate(ref SystemState state)
      {
          // КАЖДЫЙ КАДР В РАНТАЙМЕ:
          foreach (var transform in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<Headquarters>())
          {
              var grid = SystemAPI.GetSingletonRW<CityGrid>();
              grid.ValueRW.Center = transform.ValueRO.Position;
              return;
          }
      }
  }
  ```
  Каменная монолитная пирамида **никогда не двигается**. Но система каждый кадр тратит такты процессора на запрос `Query`, чтобы перезаписать `(0, 0, 0)` в центр сетки.

### 1.2. Пирамиду насильно назвали «Зданием» (`Building { Id = 1 }`)
В [`HeadquarterAuthoring.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Authoring/City/HeadquarterAuthoring.cs#L20-L23) Пирамиде присвоен компонент обычного дома:
```csharp
AddComponent(entity, new Building { Id = 1 });
```
Но у Пирамиды нет типа здания, нет рецептов маиса, ее нельзя сносить, рабочие не привязываются к ней через обычные слоты.  
В результате по всей кодовой базе расставлены **костыли-исключения**:
1. [`ConsumeAssignWorkerCommandsSystem.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Simulation/Agents/CommandSystems/ConsumeAssignWorkerCommandsSystem.cs#L52): `if (em.HasComponent<Headquarters>(buildingEntity)) return false;`
2. [`ConsumeSetWorkplacePausedCommandsSystem.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Simulation/Agents/CommandSystems/ConsumeSetWorkplacePausedCommandsSystem.cs): проверка на `Headquarters`, чтобы не выключить пирамиду.
3. [`AdvanceAgentCommuteSystem.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Simulation/Agents/Systems/AdvanceAgentCommuteSystem.cs): защита от отправки жителей на работу в пирамиду.
4. [`ClaimConstructionCrewSystem.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Simulation/Agents/Systems/ClaimConstructionCrewSystem.cs): строители не должны пытаться строить пирамиду.
5. [`SyncWorkplaceLoadSystem.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Simulation/Agents/Systems/SyncWorkplaceLoadSystem.cs): пирамида исключается из подсчета рабочих мест.
6. [`ConsumeDeconstructBuildingCommandsSystem.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Simulation/City/CommandSystems/ConsumeDeconstructBuildingCommandsSystem.cs): запрет сноса пирамиды игроком.
7. [`ConsumeDespawnBuildingsSystem.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Simulation/City/CommandSystems/ConsumeDespawnBuildingsSystem.cs#L37): `ComponentType.Exclude<Headquarters>()` при сносе домов рана.
8. [`BuildingProductionSystem.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Simulation/Economy/Systems/BuildingProductionSystem.cs): пропуск пирамиды при выработке ресурсов.
9. [`BuildingViewBoard.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Presentation/City/BuildingViewBoard.cs): специальный фильтр, чтобы не спавнить префаб кухни поверх пирамиды.
10. [`BuildingInspectPanel.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Presentation/GameHud/BuildingInspectPanel.cs): запрет открытия карточки дома при клике на пирамиду.

### 1.3. Катастрофический поиск в инспекторе пирамиды
В [`PyramidInspectPanel.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Presentation/GameHud/PyramidInspectPanel.cs#L446-L463) метод `TryFindHq` вызывается **в `Update()` каждый кадр**, пока открыта панель:
```csharp
static bool TryFindHq(EntityManager em, int id, out Entity entity)
{
    entity = Entity.Null;
    using var query = em.CreateEntityQuery(
        ComponentType.ReadOnly<Building>(),
        ComponentType.ReadOnly<Headquarters>());
    using var entities = query.ToEntityArray(Allocator.Temp);
    using var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
    for (var i = 0; i < buildings.Length; i++) {
        if (buildings[i].Id != id) continue;
        entity = entities[i];
        return true;
    }
    return false;
}
```
Каждые 16 миллисекунд создается `EntityQuery`, аллоцируются два `NativeArray` и крутится цикл ради поиска объекта, который **в игре существует в единственном экземпляре**!

### 1.4. Шизофрения визуального представления
На одну Пирамиду заведено **три разных игровых объекта**:
1. 3D-модель Пирамиды на сцене `Game.unity`.
2. Невидимый пустой GameObject `HeadquarterAuthoring` в саб-сцене `Simulation.unity`.
3. Префаб `_HqOverlay.prefab` (`ClickProxy`), который динамически спавнится поверх пирамиды ради ловли кликов мыши.

---

## 2. Целевая архитектура Великой Пирамиды

Пирамида — это **не рядовое здание `Building`**. Это **Сердце Поселения (City Core)**.

```text
                           ВЕЛИКАЯ ПИРАМИДА (City Core)
                                         │
        ┌────────────────────────────────┼────────────────────────────────┐
        ▼                                ▼                                ▼
[Священный Алтарь]               [Центр Сетки]                     [Главная Плаза]
- DivineAltar (Вера / Пламя)     - Центр (0,0,0)                   - Точка сбора свободных
- PyramidBurner (Дань богам)     - Задается 1 раз при старте         жителей (Idle plaza)
- Управление данью (слайдеры)    - Никаких систем в Update
```

### 2.1. Снятие компонента `Building`
* Пирамида **не имеет** компонента `Building`.
* Пирамида идентифицируется собственным уникальным тегом-синглтоном: `Headquarters` (или `SacredPyramid`).
* **Результат:** Из всех 10 систем симуляции (`BuildingProduction`, `AssignWorker`, `ClaimCrew`, `Despawn`...) навсегда вычищаются костыли `Exclude<Headquarters>` и `if (HasComponent<Headquarters>) return`. Системы домов становятся чистыми и занимаются только домами.

### 2.2. Центр города — константа `(0, 0, 0)`
Полярная сетка города майя радиально расходится из центра карты.
* Центр сетки `CityGrid.Center` равен `Vector3.zero` (или координатам точки сценария).
* Он задается **ровно один раз** при создании сетки в `SimulationBootstrap.InitializeRun`.
* Системы `HeadquartersCenterBakeSystem` и `SyncCityCenterFromHeadquartersSystem` **удаляются под корень**.

### 2.3. Мгновенный спавн в `SimulationBootstrap` (Без саб-сцены)
Пирамида создается обычным C#-кодом на старте рана за 0.0001 секунды:

```csharp
namespace TheyWillDescend.Simulation.Session
{
    public static class SimulationBootstrap
    {
        public static Entity SpawnCityCore(EntityManager em, Entity session, ScenarioDefinition scenario)
        {
            // 1. Создаем сущность Пирамиды
            var hq = em.CreateEntity();
            em.AddComponentData(hq, new Headquarters());
            em.AddComponentData(hq, new LocalToWorld { Value = float4x4.identity });

            // 2. Буфер дани (сжигание ресурсов)
            var feed = em.AddBuffer<PyramidFeedLine>(hq);
            // Заполняем слоты дани из ресурсов, где CanFeed == true
            
            // 3. Блокируем центральные клетки сетки под площадь (OccupiedCell)
            OccupyPlazaFootprint(em, session);

            return hq;
        }
    }
}
```

### 2.4. Инспекция и клик в UI за $O(1)$
* **Клик по пирамиде:** На 3D-модель Пирамиды в сцене вешается обычный коллайдер и простой скрипт `PyramidView : MonoBehaviour, IPointerClickHandler`. При клике он вызывает `PyramidInspectPanel.Open()`. Больше не нужен костыльный оверлей `_HqOverlay`.
* **Доступ в `PyramidInspectPanel`:**  
  Вместо ежекадрового создания `EntityQuery` и `ToComponentDataArray`:
  ```csharp
  // Мгновенный доступ к единственной пирамиде за O(1):
  if (SystemAPI.TryGetSingletonEntity<Headquarters>(out var hqEntity))
  {
      // Считываем буфер дани и веру напрямую
  }
  ```
  Ноль аллокаций, ноль циклов, ноль задержек.

---

## 3. Чеклист рефакторинга Великой Пирамиды

1. **Удалить паразитные системы:**
   * ❌ Удалить `SyncCityCenterFromHeadquartersSystem.cs`
   * ❌ Удалить `HeadquartersCenterBakeSystem.cs`
   * ❌ Удалить `PyramidFeedBakeSystem.cs`
2. **Очистить системы симуляции:**
   * Из `ConsumeAssignWorkerCommandsSystem.cs` удалить проверку на `Headquarters`.
   * Из `ConsumeSetWorkplacePausedCommandsSystem.cs` удалить проверку на `Headquarters`.
   * Из `BuildingProductionSystem.cs` удалить проверку на `Headquarters`.
   * Из `ConsumeDespawnBuildingsSystem.cs` убрать `Exclude<Headquarters>()`.
   * Из `AdvanceAgentCommuteSystem.cs` и `ClaimConstructionCrewSystem.cs` убрать проверки на `Headquarters`.
3. **Перевести спавн на `SimulationBootstrap`:**
   * Внедрить метод `SpawnCityCore` в `SimulationBootstrap.cs`.
   * Удалить префаб `HeadquarterAuthoring` из саб-сцены `Simulation.unity`.
4. **Оптимизировать UI инспектора:**
   * В [`PyramidInspectPanel.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Presentation/GameHud/PyramidInspectPanel.cs) заменить метод `TryFindHq` на синглтон-запрос `SystemAPI.GetSingletonEntity<Headquarters>()`.
   * Убрать префаб-костыль `_HqOverlay.prefab`.
5. **Проверить рестарт рана:**
   * При перезапуске игры мир уничтожается полностью (`DestroyEntity(UniversalQuery)`) и заново создается через `SimulationBootstrap.InitializeRun`.
   * Больше не нужно писать исключения для пирамиды при ресете сессии.

---

## Итог реформы Пирамиды

| Аспект | До рефакторинга | После рефакторинга |
| :--- | :--- | :--- |
| **Сущность** | Притворяется обычным `Building { Id = 1 }` | Самостоятельное Сердце Города (`Headquarters`) |
| **Системы домов** | 10 систем содержат костыли `if (Headquarters)` | 0 проверок, системы домов занимаются только домами |
| **Центр сетки** | Перезаписывается каждый кадр в Update | Задается 1 раз при старте рана в точке `(0,0,0)` |
| **Паразитные системы** | `SyncCityCenter...` жрет такты каждый кадр | Удалена под корень |
| **Инспектор UI** | `CreateEntityQuery` и аллокации NativeArray каждый кадр | Прямой синглтон-доступ за $O(1)$ без аллокаций |
| **Визуал** | 3 объекта (меш, пустышка саб-сцены, оверлей) | 1 чистый GameObject на сцене со своим кликом |
| **Рестарт рана** | Нельзя сбросить мир из-за запеченной пирамиды | Полный мгновенный сброс мира в 1 строчку |
