# 01. Инвентарь: как устроен мир сейчас

← [[00 Agent Brief]] | [[Index]]

Снято с кода. Пути от `TheyWillDescend/Assets/_Project/Scripts/`. Если файл ниже не находится — ищи в `Presentation/SimBridge/` (бывший `Application`).

---

## 1. Старт и стоп рана (сейчас несколько кадров)

### 1.1. Новая игра (меню или Play на Game)

```text
GameEntry  (loading task)
  GameSession.Begin(launch)
    SimulationBootstrap.InitializeRun(em, catalogs…)
      // ПЕРЕИСПОЛЬЗУЕТ существующую SimSession, EnsureComponent/EnsureBuffer
      // Phase = Unprepared, Mode = Off, NextBuildingId = 1
      // каталоги Base* + resolved копии (пока без сложности)
      // HQ, прототип агента, StoryBake, сетка
    WaitUntilSimulationReady()          // до 30 с, сообщение ещё про SubScene
    if Slot:  RunSessionSnapshot.BeginApply → Wait Ready
    else:     RunPublisher.BeginRun(scenario, difficulty, techs)
                Phase = Preparing
                DespawnAllAgents + DespawnAllBuildings в буферы
                RebuildResolvedCatalog + ApplyDifficulty
                PendingScenarioPlace + PendingScenarioSpawns
                ResearchWorld.Populate
              WaitForPhaseAsync(Ready)
GameEntry.Arm  → Mode = Running, SessionInGame = 1, музыка, пауза-меню
```

`WaitUntil*` крутится UniTask'ом, пока ECS-системы за кадры не опустошат очереди. `FinalizeSimSessionLifecycleSystem` ставит `Preparing → Ready`, когда `AreLifecycleQueuesDrained`.

### 1.2. Что происходит на кадрах Preparing

Порядок внутри `CommandSystemGroup` (явные `UpdateBefore`/`After`):

```text
ConsumeSimClockCommandsSystem          OrderFirst
ConsumeDespawnAgentsSystem             после clock
ConsumeDespawnBuildingsSystem          после агентов
ConsumePendingScenarioSpawnsSystem     после зданий
  // Application.isPlaying — ЕДИНСТВЕННЫЙ UnityEngine.Application в Simulation
  // создаёт N SpawnAgentRequest на плазе, Workers = 0
ConsumeSpawnAgentCommandsSystem        инстанс прототипа
ConsumePlaceBuildingCommandsSystem
  DrainPendingScenario  → Place(..., InstantComplete=1, Source=Setup)
  DrainRequests         → Gameplay только если IsReady;
                          SnapshotRestore только если Preparing
ConsumePlaceRoadStrokeSystem
ConsumeDemolishRoadSystem
ConsumeDeconstructBuildingCommandsSystem   // НЕТ UpdateAfter — порядок относительно Place не зафиксирован
ConsumeSetPyramidFeedCommandsSystem
ConsumeSetActiveResearchCommandsSystem
FinalizeSimSessionLifecycleSystem      Preparing→Ready если очереди пусты
ApplySimDeltaTimeSystem                OrderLast
```

Узкое место: `ConsumeDeconstructBuildingCommandsSystem` не привязан по порядку к Place. В одном кадре «поставить и снести» — гонка. После Ф1 гонка останется, пока Ф4 не поставит явный `UpdateAfter(Place)`.

### 1.3. Выход / рестарт

```text
GameEntry teardown
  Disarm
  GameSession.Shutdown
    RunPublisher.BeginReset
      ResearchWorld.DestroyAll
      Phase = Resetting
      DespawnAllAgents + DespawnAllBuildings
    WaitForPhaseAsync(Unprepared)     // снова до 30 с
```

Дома и агенты **не** в SubScene: они в default world. Выгрузка сцены их не гарантирует — поэтому и нужен wipe. Сейчас wipe = две despawn-команды + ждать системы.

`SimulationBootstrap.InitializeRun` при втором ране **не уничтожает** старых агентов/дома сам: он перезаписывает компоненты сессии. Очистку делает только очередь despawn из `BeginRun`/`BeginReset`/`BeginApply`.

### 1.4. Загрузка сейва

`RunSessionSnapshot.BeginApply`:

1. `TryRestoreResolvedCatalog` — перезаписывает resolved-буферы из JSON (не Base*).
2. `Phase = Preparing`, clock Off.
3. `SimClockCommand.Restore` + despawn all.
4. На **каждый** дом — `SimCommands.Request(PlaceBuildingRequest)` с полями восстановления: `BuildingId`, `ConstructionElapsed/Duration`, `InstantComplete`, `Dismantling`, `DesiredWorkers`, `Paused`, `Source = SnapshotRestore`.
5. На каждого агента — `SpawnAgentRequest` с `WorkplaceBuildingId`, позой, плазой. **`ConstructionBuildingId` в сейве нет** — бригада стройки после загрузки теряется.
6. Ресурсы, боги, `ResearchWorld.Populate` + прогресс, `StorySave.QueueRestore`.

Consumer на Preparing принимает только `Source == SnapshotRestore`. Gameplay-place в этот кадр отбрасывается.

Оплата: `TryPay` пропускает, если `InstantComplete != 0` **или** `BuildingId > 0`. Поэтому восстановление не снимает ресурсы повторно.

---

## 2. Что висит на сессионной сущности

Сессия = singleton с `SimSession`. Кэш: `SimWorld._bag`, инвалидация через `em.Exists(_bag)`.

Компоненты (ставит bootstrap, если не указано иное):

| Тип | Файл | Зачем |
| --- | --- | --- |
| `SimSession` | `Session/Components/SimSession.cs` | `Phase` |
| `SimControl` | `Session/Components/SimControl.cs` | Mode, Speed, DeltaTime, SessionInGame, TimePaused, PlayerPaused, BuildLocked |
| `GameTime` | `Time/Components/GameTime.cs` | день, смена |
| `AgentIdSequence` | `Agents/Components/AgentIdSequence.cs` | счётчик агентов |
| `PendingScenarioSpawns` | `Agents/Components/PendingScenarioSpawns.cs` | `Workers` на старт |
| `CityGrid` | `City/Components/CityGrid.cs` | Config, Center, **NextBuildingId**, Ready |
| `RoadNetwork` | `City/Components/RoadNetwork.cs` | NextSegmentId, SectionLength |
| `SimPrototypes` | `Session/Components/SimPrototypes.cs` | Entity прототипа агента |
| `PyramidConfig` | `Gods/Components/PyramidConfig.cs` | час смены эры, cap, decay |
| `Timeline` | `Gods/Components/Timeline.cs` | EraIndex, lerp max |
| `GodLoyalty` | `Gods/Components/GodLoyalty.cs` | 0–100 |

Буферы на сессии:

| Буфер | Зачем |
| --- | --- |
| `SimClockCommand` | поток часов |
| `DespawnAllAgentsCommand` | wipe агентов |
| `DespawnAllBuildingsCommand` | wipe домов |
| `PendingScenarioPlace` | дома сценария |
| `OccupiedCell` | занятость клеток (`BuildingId`) |
| `RoadSegment` | рёбра дорог (`SiteId` = Building.Id площадки) |
| `BaseBuildingPrototype/Cost/Recipe` | «сырой» каталог со штампа |
| `BuildingPrototype/Cost/Recipe` | resolved + сложность |
| `BuildingRejectedEvent` | отказы стройки для UI |
| `ResourceAmount`, `ResourceInfo` | склад |
| `EraLine`, `EraTributeLine` | эры |
| плюс story-буферы с `StoryBake` | |

Отдельные сущности, не сессия:

| Сущность | Как узнать | Важно для wipe |
| --- | --- | --- |
| HQ | `Headquarters` singleton | `EnsureHeadquarters` переиспользует; буфер `PyramidFeedLine` |
| Прототип агента | `SimPrototypes.Agent`, есть `Prefab` | `InitializeRun` тоже переиспользует |
| Research board | `ResearchControl` singleton | `ResearchWorld.Populate` делает DestroyAll + create |
| TechCard | по одной на технологию | DestroyAll в Populate |
| Дома | `Building` | только despawn-команда |
| Агенты | `AgentId` (не Prefab) | только despawn-команда |
| Request'ы | одноразовые entity | если Begin прервать — могут остаться |
| StoryLiveDialog / Quest | живые карточки | StoryBake / AdvanceStorySystem |

`HasLifecycleQueues` проверяет ~20 типов на сессии. Забыл буфер — старт рана вечно ждёт таймаут.

---

## 3. Идентичность сейчас — везде `int`

| Место | Поле |
| --- | --- |
| `Building.Id` | ключ дома |
| `CityGrid.NextBuildingId` | генератор (на сетке — не канон) |
| `AgentAssignment.WorkplaceBuildingId` | 0 = свободен |
| `AgentAssignment.ConstructionBuildingId` | 0 = нет стройки |
| `OccupiedCell.BuildingId` | кто занял клетку |
| `RoadSegment.SiteId` | площадка дороги |
| `DemolishBuildingRequest.BuildingId` | |
| `SpawnAgentRequest.WorkplaceBuildingId`, `AgentId` | |
| `PlaceBuildingRequest.BuildingId` | только restore |
| `BuildingSelection.SelectedBuildingId` / `HoveredBuildingId` | UI |
| `BuildingIdTag.Id` | на виде, клик |

Поиск дома по id: `BuildingDismantle.TryGetBuilding`, `BuildingInspectPanel.TryFindBuilding`, списки `HouseRef`/`Site` в commute и claim.

---

## 4. Тик после CommandSystemGroup

```text
AdvanceGameTimeSystem
ClaimConstructionCrewSystem          ближайшие свободные → площадка; SortById
WorkforceDispatchSystem              НЕТ проверки SimControl.IsRunning
AdvanceAgentCommuteSystem            линейный поиск HouseRef по id
AdvancePlazaIdleSystem
AdvanceAgentLocomotionSystem
AdvanceConstructionSystem            после commute (нужен Arrived)
SyncWorkplaceLoadSystem              AssignedCount + WorkingCount из агентов
BuildingProductionSystem             шкала WorkingCount/slots; skip IsPaused
AdvanceTimelineSystem
AdvanceResearchSystem
PyramidBurnSystem
AdvanceStorySystem
```

`WorkforceDispatchSystem` работает при `Mode == Off` (меню, shutdown). Узкое место Ф3: добавить гейт `Off`, на паузе игрока выравнивать штат можно.

Два источника факта штата: диспетчер считает current сам; `SyncWorkplaceLoadSystem` пересчитывает `AssignedCount`/`WorkingCount` позже. Диспетчер `AssignedCount` не пишет.

---

## 5. Входы игрока сейчас

`SimCommands`:

- `TryPost<T>` — буфер на `_bag`; если буфера нет — **поиск любой сущности с этим буфером** (fallback). Убрать в Ф4.
- `Request<T>` — `CreateEntity` + компонент. **Не вешает тег владения** — при обрыве старта entity останется.
- `RequestRoadStroke` / `RequestDemolishRoad` — спец. случаи.

Presentation пишет **напрямую** в ECS:

| Что | Кто |
| --- | --- |
| `Workplace.DesiredWorkers` ± / max | `BuildingInspectPanel` |
| `Workplace.Paused` toggle | `BuildingInspectPanel` |
| `GameTime` (читы) | `CheatButtons` |

Остальное — request или `TryPost(SimClockCommand)`.

Пирамида: UI → `SetPyramidFeedRequest` → consumer → `PyramidFeed.SetPerHour`. Не трогать в этих фазах.

---

## 6. Presentation: тяжёлые чтения

Каждый кадр, пока открыта карточка:

`BuildingInspectPanel.Update` — `TryFindBuilding` (полный проход домов), `CountWorkforce` (агенты + workplaces, «все минус сумма желаний»), `CountConstructionCrew`, `TryGetGameTime`, при необходимости `FindAnyObjectByType<BuildPlacementController>`.

`PyramidInspectPanel.Update` — `CreateEntityQuery<Headquarters>`.

`AgentViewBoard.LateUpdate` — несколько `ToComponentDataArray` (ключ вида = `AgentId`).

`BuildingViewBoard` — запрос `Building`, словарь по `building.Id`.

`BuildingSelection` кэширует `EntityQuery` на MonoBehaviour (время жизни GO ≠ World).

---

## 7. Известные дыры сейва (не цель фаз, знать)

- Дороги не пишутся в `RunSnapshot` — после загрузки дорог нет.
- `AgentSnapshot` без `ConstructionBuildingId` — бригада стройки сбрасывается.
- `RunSnapshot.CurrentVersion = 26`. Старые слоты удаляются, миграций нет. **Любое изменение формы JSON — бамп версии.**

---

## 8. Файлы, которые Ф1 почти наверняка тронет

```text
Presentation/Shell/Session/GameSession.cs
Presentation/Shell/Session/GameEntry.cs          (teardown: ждать Unprepared → просто Wipe)
Presentation/SimBridge/SimulationBootstrap.cs
Presentation/SimBridge/RunPublisher.cs           (распилить / сузить)
Presentation/SimBridge/RunSessionSnapshot.cs
Presentation/SimBridge/StorySave.cs
Simulation/Session/Components/SimSession.cs
Simulation/Session/Components/SimSessionAccess.cs
Simulation/Session/CommandSystems/FinalizeSimSessionLifecycleSystem.cs
Simulation/Session/Commands/SimCommands.cs       (тег на Request)
Simulation/Agents/CommandSystems/ConsumeDespawnAgentsSystem.cs          удалить
Simulation/Agents/CommandSystems/ConsumePendingScenarioSpawnsSystem.cs  удалить
Simulation/City/CommandSystems/ConsumeDespawnBuildingsSystem.cs         удалить
Simulation/City/CommandSystems/ConsumePlaceBuildingCommandsSystem.cs    вынести Spawn
Simulation/Research/ResearchWorld.cs             RunScoped + не ждать CommandsDrained
Simulation/Stories/StoryBake.cs                  RunScoped
```
