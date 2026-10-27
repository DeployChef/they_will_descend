# 02. Ф1 — Чистый мир рана

← [[01 Inventory]] | [[Index]] | далее [[03 F2 Entity Identity]]

Цель: `Begin` и `Shutdown` **синхронны по ECS**. Нет фаз, нет ожидания очередей, нет despawn-команд. Первый кадр после `Begin` уже показывает дома сценария и людей на плазе.

Игровая логика производства/движения не меняется. Меняется только **как мир появляется и исчезает**.

---

## 1. Было → станет

### Старт

```text
БЫЛО
  InitializeRun (reuse session)
  ждать HasLifecycleQueues (до 30 с, текст про SubScene)
  BeginRun: Phase=Preparing, despawn×2, очереди сценария
  системы 1–N кадров
  Finalize: Ready
  Arm

СТАНЕТ
  em.CompleteAllTrackedJobs()
  RunWorld.Wipe(em)
  SimulationBootstrap.InitializeRun(...)     // всегда НОВАЯ сессия, каталог УЖЕ со сложностью
  if Slot: SnapshotRestore.Apply(em, snapshot)
  else:    ScenarioSetup.Apply(em, scenario) // дома сразу, рабочие сразу
  // Phase больше нет. Mode пока Off — Arm как сейчас в GameEntry
```

### Стоп

```text
БЫЛО  BeginReset + ждать Unprepared
СТАНЕТ
  GameEntry.Disarm
  em.CompleteAllTrackedJobs()
  RunWorld.Wipe(em)
```

`GameSession.Begin` может остаться `async UniTask` ради загрузки сейва с диска и UI. Внутри не должно быть `WaitUntil` по ECS.

Структурные изменения из MonoBehaviour между кадрами допустимы: системы в этот момент не исполняются, `CompleteAllTrackedJobs` закрывает джобы.

---

## 2. Тег владения `RunScoped`

Новый компонент, Simulation/Session:

```csharp
namespace TheyWillDescend.Simulation.Session
{
    /// <summary>Всё, что умрёт вместе с раном. Прототипы несут тег — инстансы наследуют.</summary>
    public struct RunScoped : IComponentData { }
}
```

Вешать:

| Сущность | Где |
| --- | --- |
| сессия | `InitializeRun` сразу после `CreateEntity` |
| HQ | `EnsureHeadquarters` — больше не переиспользовать старый HQ |
| прототип агента | `EnsureAgentPrototype` — больше не переиспользовать; тег на прототипе → `Instantiate` копирует |
| research board + TechCard | `ResearchWorld.Populate` |
| story live + то, что печёт `StoryBake` | `StoryBake.Apply` |
| дом | `SpawnHouse` |
| request | `SimCommands.Request` / `RequestRoadStroke` |
| агент | наследует от прототипа; если `ConsumeSpawnAgentCommandsSystem` создаёт не через Instantiate — повесить вручную |

Wipe:

```csharp
var query = em.CreateEntityQuery(new EntityQueryDesc
{
    All = new[] { ComponentType.ReadOnly<RunScoped>() },
    Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
});
em.DestroyEntity(query);
```

**Нельзя** `DestroyEntity(UniversalQuery)`.

После wipe `SimWorld._bag` невалиден: `Exists` вернёт false, следующий `TryGet` найдёт новую сессию. Версия Entity при уничтожении растёт — коллизии со старым `_bag` не будет.

---

## 3. Bootstrap больше не reuse

Сейчас `InitializeRun` ищет `SimSession`, берёт первую, лишние убивает, на оставшейся `Ensure*`. Из-за этого второй ран может унаследовать мусор в буферах, если despawn не отработал.

Целевое правило: **после Wipe сессии нет**. `InitializeRun` всегда `em.CreateEntity()`. Поиск «уже есть сессия» — только assert/лог в редакторе, не рабочий путь.

`EnsureHeadquarters` / `EnsureAgentPrototype`: создать новые, не искать старые (их уничтожил wipe). Если wipe забыли — лучше упасть, чем склеить два рана.

`GameTime` сейчас может жить на сессии или отдельно (bootstrap убивает чужие GameTime). Держать `GameTime` **на сессии**, не на второй сущности.

---

## 4. Каталог один раз, уже со сложностью

Сейчас:

1. Bootstrap пишет Base* и копии resolved.
2. `RunPublisher.RebuildResolvedCatalog` копирует Base → resolved.
3. `ApplyDifficulty` правит resolved bool-флагами.

Целевое:

- `InitializeRun(..., DifficultyProfile difficulty)` сразу пишет **итоговые** `BuildingPrototype` / `Cost` / `Recipe`.
- Код оверлея перенести из `RunPublisher.ApplyDifficulty` в bootstrap (или маленький `DifficultyApply` рядом). Форма оверлея (bool replace*) не меняется — CMS позже.
- Удалить `BaseBuildingPrototype`, `BaseBuildingCatalogCost`, `BaseBuildingCatalogRecipe` и `RebuildResolvedCatalog`.
- Сейв по-прежнему кладёт в JSON **resolved** каталог (как сейчас `CaptureResolvedCatalog`). Restore пишет прямо в те же буферы.

Проверка: `GameSession.IsSimulationReady` сегодня требует `BaseBuildingPrototype.Length > 0`. После удаления Base готовность = «сессия существует, CityGrid.Ready, прототип агента не Null». Отдельный Wait не нужен: `InitializeRun` возвращает, когда это уже правда.

---

## 5. Сценарий — прямой спавн

Вынести из consumer'а чистую функцию размещения (она же понадобится запросу игрока и сейву):

```text
Simulation/City/BuildingPlacement.cs   (статический класс)
  TryValidate(...)  → reason или ok (unlock, клетка, overlap, дороги, оплата)
  Spawn(...)        → Entity дома, occupancy, id, компоненты как сейчас SpawnHouse
```

Правила валидации зависят от **режима**:

| Режим | Оплата | Unlock | Occupancy |
| --- | --- | --- | --- |
| Gameplay | да | да | да |
| Setup (сценарий) | нет | нет | да (иначе два дома в одной клетке — ошибка контента, лог) |
| Restore | нет | нет | да; id из сейва |

`PlaceBuildingRequest` после этого содержит только выбор игрока:

```csharp
public struct PlaceBuildingRequest : IComponentData
{
    public FixedString64Bytes TypeId;
    public int AnchorCluster;
    public int AnchorRadial;
}
```

Удалить: `PlaceBuildingCommandSource`, `BuildingId`, `WidthClusters`, `DepthRadialRings` (брать из каталога), поля стройки, `DesiredWorkers`, `Paused`.

`ConsumePlaceBuildingCommandsSystem` (имя можно оставить до Ф4): цикл по request → `TryValidate(Gameplay)` → `Spawn`. Не читает `SimSession.Phase`.

`ScenarioSetup.Apply`:

- стоки как сейчас `WriteStock`;
- для каждой записи сценария `Spawn(..., InstantComplete, DesiredWorkers=0)`;
- рабочих создать напрямую (логика позы с плазы из `ConsumePendingScenarioSpawnsSystem.Run`, но без request-сущностей и без `Application.isPlaying`).

Удалить `PendingScenarioPlace`, `PendingScenarioSpawns`, `ConsumePendingScenarioSpawnsSystem`.

---

## 6. Сейв — прямой спавн

`SnapshotRestore.Apply(em, snapshot)` (бывший `BeginApply` без очередей):

1. Restore resolved-каталога (как сейчас).
2. `ResearchWorld.Populate` + прогресс (как сейчас `ApplyResearch`).
3. Часы/лояльность/эра/стоки/пирамида — **прямая запись** в компоненты. Не `SetPyramidFeedRequest`. Не `SimClockCommand.Restore` — выставить `SimControl.Speed` и `TimePaused` полями; `Mode` пусть ставит `GameEntry.Arm` как сейчас.
4. Дома: для каждой записи `BuildingPlacement.Spawn` с сохранёнными `Building.Id`, `Construction`, `Workplace.DesiredWorkers/Paused`.
5. `NativeHashMap<int, Entity>` id → entity.
6. Агенты: Instantiate прототипа, выставить позу, `AgentAssignment.WorkplaceBuildingId` из сейва (ещё int — это Ф2). Если workplace id не найден в карте — 0.
7. `StorySave` восстановить напрямую, не через очередь, если очередь ждала Ready. Если `QueueRestore` только пишет буферы, которые `AdvanceStorySystem` читает на Ready — после Ф1 Ready нет, система должна работать при `SessionInGame`/`IsRunning`. **Проверить StorySave.QueueRestore** и не оставить восстановление, которое ждёт фазы.

`CityGrid.NextBuildingId` после restore = max(id)+1.

Роадмап: дороги в сейве по-прежнему нет. Не добавлять формат дорог «заодно», если пользователь не просил — это бамп версии и отдельная задача. Не сломать существующие поля.

Бамп `RunSnapshot.CurrentVersion`, если убрали/добавили поле. Сейчас 26.

---

## 7. Фазы и despawn — снести

Удалить полностью:

- enum `SimSessionPhase`, поля `Phase`, `AcceptsSetupCommands`, `IsReady`
- `FinalizeSimSessionLifecycleSystem`
- `SimSessionAccess.HasLifecycleQueues`, `AreLifecycleQueuesDrained`
- `ResearchWorld.CommandsDrained` (использовался только в drained)
- `DespawnAllAgentsCommand` + `ConsumeDespawnAgentsSystem`
- `DespawnAllBuildingsCommand` + `ConsumeDespawnBuildingsSystem`
- `GameSession`: `simulationReadyTimeoutSeconds`, `WaitUntilSimulationReady`, `WaitForPhaseAsync`, `AwaitCondition`, `IsSimulationReady`, `IsSessionPhase`
- `RunPublisher.BeginReset`, `ClearLifecycleQueues`, `RebuildResolvedCatalog`

`SimSession` как маркер сессионной сущности можно оставить пустым `IComponentData` (удобный singleton). Либо заменить запросом `RunScoped`+конкретные синглтоны — решать по месту; проще оставить пустой `SimSession`.

Gating gameplay-request: вместо `lifecycle.IsReady` → `SimControl.SessionInGame != 0` (ставит `GameEntry.Arm`). Пока Arm не вызван, Mode=Off, запросы игрока не принимаются. Сценарий/сейв вызываются **до** Arm из bootstrap, минуя request.

`ConsumeDeconstructBuildingCommandsSystem` сейчас требует `IsReady`. После Ф1 — `SessionInGame`.

---

## 8. Порядок работ (делать сверху вниз)

1. Добавить `RunScoped` и `RunWorld.Wipe`. Пока никого не тегать — тесты не сломаются.
2. Вынести `BuildingPlacement.TryValidate` + `Spawn` из `ConsumePlaceBuildingCommandsSystem`, consumer вызывает их. Play: поставить дом как раньше.
3. Навесить `RunScoped` на всё из таблицы §2. `SimCommands.Request` тоже. Play.
4. Переключить `InitializeRun` на create-only + difficulty внутрь. Удалить Base*-буферы; починить все `HasBuffer<Base*>` (GameSession.IsSimulationReady — удалится шагом 6).
5. Написать `ScenarioSetup.Apply`, подключить вместо очередей. Удалить pending-типы и `ConsumePendingScenarioSpawnsSystem`. Play: Debug-сценарий сразу с домами и людьми **в первом кадре** после скрытия loading.
6. Написать `SnapshotRestore.Apply`. Урезать `PlaceBuildingRequest`. Удалить фазы, despawn, WaitUntil. `Shutdown` = Wipe. `GameEntry` teardown больше не ждёт Unprepared.
7. Grep-ворота §10. Чек-лист Play. Вики.

Не делать в Ф1: смену `WorkplaceBuildingId` на `Entity` — это Ф2. После Ф1 сейв всё ещё хранит int id, restore пишет int на агента.

---

## 9. Узкие места

**Stories.** `StoryBake.Apply`, live dialog/quest, `StorySave.QueueRestore` завязаны на те же очереди и Ready. Если оставить restore «на следующий тик через request», после удаления фаз он никогда не применится. В Ф1 stories только: тег, wipe, прямое восстановление. Не переписывать `AdvanceStorySystem`.

**Research.** `Populate` уже DestroyAll+create. Добавить `RunScoped` на board и карты. `DestroyAll` в начале Populate остаётся (повторный Populate внутри рана). Wipe рана уничтожит их через тег — `BeginReset` больше не зовёт `DestroyAll` отдельно, но `Shutdown` wipe и так их убьёт. `Begin` нового рана: Wipe (убил старые карты) → Populate (создал новые). Не вызвать Populate до Wipe — будут две доски.

**HQ.** Сейчас переиспользуется. После wipe его нет. `PyramidView` в сцене — GameObject, не ECS; клик идёт по коллайдеру сцены. Не привязывать GO к Entity HQ. Инспектор пирамиды ищет HQ запросом — после Ф1 сработает, пока сущность одна. `CityCore` заведём в Ф5.

**Прототип агента.** Если забыть `IncludePrefab` в wipe-запросе, прототип переживёт ран, `EnsureAgentPrototype` найдёт труп/старый entity. Всегда IncludePrefab.

**Недообработанные request.** Игрок кликнул Place в последний кадр, сразу Exit. Request без `RunScoped` останется в мире и выполнится в следующем ране. Поэтому тег в `SimCommands.Request` обязателен в том же PR, что wipe.

**`Application.isPlaying` в Simulation.** Уходит вместе с `ConsumePendingScenarioSpawnsSystem`. После Ф1 grep `UnityEngine.Application` по `Simulation/` должен быть пуст. Это закрывает хвост Ф6.

**Доски видов.** После Wipe запрос домов/агентов пуст — `BuildingViewBoard`/`AgentViewBoard` должны уничтожить GO (у них это уже есть на пустом query). Проверить сценарий «меню → новая игра» без перезапуска Play: нет двойных домов-призраков.

**LoadingTasks.** `Begin` больше не ждёт кадры ECS. Он всё ещё async (сейв с диска). Не ставить искусственный `UniTask.Yield` «на всякий случай».

**RunPublisher.** После Ф1 либо удалить, либо оставить фасадом `BeginRun = ScenarioSetup` без очередей. Не оставлять пустой класс «на потом».

---

## 10. Grep-ворота (пусто после фазы)

```
SimSessionPhase
AcceptsSetupCommands
DespawnAll
PendingScenario
BaseBuilding
PlaceBuildingCommandSource
WaitUntilSimulationReady
WaitForPhaseAsync
HasLifecycleQueues
AreLifecycleQueuesDrained
CommandsDrained
BeginReset
RebuildResolvedCatalog
```

В `Simulation/`: `UnityEngine.Application`.

`PlaceBuildingRequest` — только `TypeId`, `AnchorCluster`, `AnchorRadial`.

---

## 11. Критерии успеха

- В `GameSession` нет WaitUntil по ECS и нет таймаута 30 с.
- «Новая игра → меню → новая игра → загрузка» три раза подряд без ошибок. В Entities Hierarchy после выхода в меню количество сущностей ≈ до первого рана (служебные пакеты + нет домов/агентов/сессии).
- Первый кадр после скрытия loading уже с домами Debug-сценария и рабочими на плазе.
- Поставить дом, назначить, сохранить, загрузить — штат и прогресс стройки на месте (бригада стройки по-прежнему не в сейве — как было).

---

## 12. Вики после Ф1

- [[../../Architecture/03 Core Systems|03]] — поток старта без фаз
- [[../../Architecture/09 App Shell|09]] и `.cursor/rules/app-shell.mdc` — teardown рана больше не async-wait
- [[../../Architecture/14 Sim Presentation Bridge|14]] — нет despawn/фаз
- [[../07 Foundation Fixes|07]] §3 — статус сделано
- Этот файл — пометка «сделано» вверху
