# 07. Базовые правки фундамента (по итогам аудита)

← [[06 Headquarters & City Core|06 Пирамида]] | [[Index|План рефакторинга]] | [[../Home|Главная вики]]

Документ фиксирует **реальное** состояние кода на 27.10.2026 и план базовых правок, без которых дальнейший рефакторинг превращается в заплатки. Он имеет приоритет над документами 01–06 там, где они расходятся с кодом: 01–06 писались до частичной миграции и местами описывают как будущее то, что уже сделано.

**Рабочий пакет для агента, который реализует Ф1–Ф5:** [[07-ECS/Index|07-ECS]] — переходы файл-за-файлом, узкие места, grep-ворота, плейбук. Этот 07 остаётся сжатым планом. Слайдеры пирамиды в пакете **не** делаются (см. §2.4).

---

## 0. Где мы сейчас

Миграция из [[04 Step-by-Step Roadmap]] выполнена примерно наполовину. Целевые паттерны внедрены, но поверх старого фундамента.

| Шаг дорожной карты | Состояние |
| --- | --- |
| 1. Runtime bootstrap вместо SubScene | SubScene из `Game.unity` убрана, `SimulationBootstrap` есть. Остались фазы, таймауты, despawn-команды |
| 2. Рабочие через desired state | `DesiredWorkers` + `WorkforceDispatchSystem` есть, команды assign/unassign удалены. Семантика сломана (см. §3) |
| 3. Стройка и снос через request-сущности | `PlaceBuildingRequest` / `DemolishBuildingRequest` есть. Внутри — поиск по `int Id` |
| 4. Очистка сессии и аллокаций | Частично |
| 5. CMS (`BuildingDefinition`) | Не начат |
| 06. Пирамида без `Building` | В основном сделан. `PyramidInspectPanel` ищет HQ запросом каждый кадр |

Уже **не** проблема (документы 03/06 устарели в этих пунктах): `DestroyImmediate` в досках защищён `Application.isPlaying`; пул агентов есть; `SyncCityCenterFromHeadquartersSystem` и `HeadquartersCenterBakeSystem` удалены; у HQ нет `Building`.

---

## 1. Порядок фаз и почему именно такой

```text
Ф0  Решения по семантике            (бумага, 0 кода)
Ф1  Чистый мир рана                 (bootstrap = wipe + create, без фаз)
Ф2  Идентичность на Entity          (ключевой камень)
Ф3  Рабочая сила: честный desired state
Ф4  Входы игрока: единые правила    (requests / буферы / поля желания)
Ф5  Чтение в Presentation
Ф6  Сборки, namespace, мусор
Ф7  Синхронизация вики и правил
```

Зависимости:

- **Ф1 раньше Ф2.** Сейчас загрузка сейва восстанавливает дома через `PlaceBuildingRequest` с `int BuildingId`, а рабочих — через `SpawnAgentRequest` с `WorkplaceBuildingId`. Если сначала перейти на `Entity`, придётся протаскивать маппинг id → Entity через очередь запросов. Если сначала сделать прямое восстановление в bootstrap, маппинг — это одна локальная `NativeHashMap<int, Entity>` внутри одного метода.
- **Ф2 раньше Ф3 и Ф5.** Почти все линейные поиски и ежекадровые запросы — следствие `int Id`. Чинить их по отдельности до смены идентичности — двойная работа.
- **Ф4 после Ф1.** Половина буферов на сессии (`PendingScenarioPlace`, `DespawnAll*Command`, `PendingScenarioSpawns`) исчезнет вместе с фазами. Унифицировать то, что останется, проще.

Каждая фаза заканчивается рабочей игрой. Между фазами — коммит и прогон чек-листа §9.

---

## 2. Ф0. Решения по семантике (подтвердить до кода)

Код и вики сейчас противоречат друг другу в трёх местах. Без решения любая реализация будет «угадыванием».

### 2.1. Что значит пауза здания

- Сейчас `WorkforceDispatchSystem` при `Paused` считает `desired = 0` и **отпускает** людей.
- Комментарий в `Workplace.Paused` и тексты `BuildingInspectPanel` («Crew stays assigned») говорят, что люди **остаются**.
- Вики: [[../Architecture/15 Refactoring — Target ECS & Interaction|15]] и [[01 ECS & Commands|01]] — отпускает; [[../Architecture/14 Sim Presentation Bridge|14]] — остаются.

> **Решено (27.10.2026):** штат остаётся. Пауза останавливает только производство: люди по-прежнему ходят на работу, но ничего не производят.

**Предлагаемое решение:** пауза = остановка рецепта, штат остаётся назначенным. Два поля — две ортогональные оси желания: `DesiredWorkers` отвечает «сколько людей», `Paused` — «работает ли рецепт». Хочешь отпустить людей — ставишь штат в 0. Тогда UI-тексты и комментарий уже верны, правится только диспетчер.

### 2.2. Кто ограничивает желание

Правило проекта: ограничение живёт в системе, UI только читает, чтобы посереть кнопку.

**Предлагаемое решение:**
- `DesiredWorkers` пишет **только** игрок (UI и восстановление сейва). Система его **никогда** не переписывает.
- Система считает эффективную цель `target = Paused ? AssignedNow : min(DesiredWorkers, slots)` и догоняет её столько, сколько есть свободных людей.
- Недобор — нормальное состояние, видимое в UI: «желаемо 5, назначено 3, не хватает людей».
- UI может серить «+», когда `DesiredWorkers >= slots`. Проверку «есть ли свободные» UI делает по готовой сводке из симуляции (§7.2), а не своим подсчётом.

### 2.3. Порядок раздачи людей при нехватке

Сейчас порядок = порядок чанков в запросе, то есть недетерминирован и меняется после структурных изменений.

**Предлагаемое решение на сейчас:** стабильный порядок по `BuildingId` (ключ сохранения, §4.4). Позже — поле приоритета на `Workplace`, тоже как желание игрока.

### 2.4. Слайдеры пирамиды

Правило `pyramid-timeline` и вики называют слайдер **желанием**. Код сделал его запросом `SetPyramidFeedRequest`.

**Решение:** желание. `PyramidFeedLine.PerHour` на HQ пишет UI напрямую. См. §6.3.

> **Отложено (27.10.2026):** слайдеры будут переписаны под другую геймдизайн-идею. Не трогать до нового дизайна.

### 2.5. Какой вход для чего

Сформулировать один раз и записать в правила (§10):

| Природа действия | Канал |
| --- | --- |
| Постоянная настройка уже существующей сущности (штат, пауза, слайдер, позже приоритет) | Поле желания на этой `Entity` |
| Разовое действие над миром (поставить, снести, заспавнить, исследовать, позже ритуал, закон) | Request-сущность |
| Упорядоченный поток команд одного вида, где важен порядок внутри кадра (часы, story UI) | Буфер на сессии |
| Наружу из симуляции (отказ стройки) | Буфер-событие на сессии, читает Presentation |

Почему часы остаются буфером: несколько команд часов за кадр (пауза, затем скорость) должны применяться строго по порядку. Порядок обхода request-сущностей в запросе не гарантирован.

---

## 3. Ф1. Чистый мир рана

> **Статус: сделано.** Подробности реализации: [[07-ECS/02 F1 Run World]].

### 3.1. Диагноз

Bootstrap стал синхронным, но вокруг него осталась вся машинерия эпохи SubScene:

- Фазы `SimSession.Phase` (`Unprepared → Preparing → Ready → Resetting`) и `FinalizeSimSessionLifecycleSystem`, который ждёт «опустошения очередей».
- `GameSession.WaitUntilSimulationReady` с таймаутом 30 секунд и сообщением про SubScene; `WaitForPhaseAsync`.
- `SimSessionAccess.HasLifecycleQueues` — проверка ~20 компонентов и буферов; `AreLifecycleQueuesDrained`.
- `RunPublisher.BeginRun` / `BeginReset` ставят в очередь `DespawnAllAgentsCommand` и `DespawnAllBuildingsCommand`, а потом ждут, пока системы их обработают.
- Двойные каталоги `BaseBuildingPrototype/Cost/Recipe` и resolved-копии плюс `RebuildResolvedCatalog` — нужны были, чтобы накладывать сложность на запечённые данные. Сейчас данные не запекаются.
- `SimulationBootstrap.InitializeRun` **переиспользует** существующую сессию (`EnsureComponent`, `EnsureBuffer`, поиск и удаление дублей `SimSession` / `GameTime`), а не создаёт мир с нуля.
- Сценарий раскладывается через очереди: `PendingScenarioPlace`, `PendingScenarioSpawns` → `ConsumePendingScenarioSpawnsSystem` (с `UnityEngine.Application.isPlaying` внутри Simulation).
- Загрузка сейва (`RunSessionSnapshot.BeginApply`) идёт через геймплейные запросы с полями восстановления (§3.4).

Итог: старт рана — асинхронный процесс на несколько кадров с таймаутами, хотя вся работа синхронна и укладывается в один вызов.

### 3.2. Целевая форма

```text
GameSession.Begin(launch)
  em.CompleteAllTrackedJobs()
  RunWorld.Wipe(em)                                   // уничтожить всё, что принадлежит рану
  SimulationBootstrap.InitializeRun(em, kit, difficulty)  // сессия, каталоги (уже со сложностью), сетка, HQ
  if launch.LoadSlot:  SnapshotRestore.Apply(em, snapshot)
  else:                ScenarioSetup.Apply(em, scenario)  // дома сразу построены, рабочие сразу на плазе
  GameRun.Arm()

GameSession.Shutdown()
  GameRun.Disarm()
  em.CompleteAllTrackedJobs()
  RunWorld.Wipe(em)
```

Никаких фаз, ожиданий, таймаутов и despawn-команд. `Begin` может остаться `async` только ради сцен и UI, не ради ECS.

Структурные изменения из MonoBehaviour между кадрами допустимы: системы в этот момент не исполняются, `CompleteAllTrackedJobs` завершает джобы.

### 3.3. Как уничтожать «всё, что принадлежит рану»

`EntityManager.DestroyEntity(em.UniversalQuery)` **нельзя**: в мире живут чужие служебные сущности (время мира, синглтоны Entities Graphics и других пакетов). Их смерть ломает мир.

Целевой приём — тег владения:

```csharp
namespace TheyWillDescend.Simulation.Session
{
    /// <summary>Destroyed on run wipe. Prototypes carry it, so instances inherit it.</summary>
    public struct RunScoped : IComponentData { }
}
```

- Bootstrap вешает `RunScoped` на сессию, HQ, прототип агента, сущности исследований и историй.
- `SpawnHouse` вешает на дом. Агенты наследуют от прототипа через `Instantiate`.
- `SimCommands.Request` вешает на request-сущность, чтобы недообработанные запросы тоже умирали при выходе.
- `RunWorld.Wipe` строит запрос `WithAll<RunScoped>` с опциями `IncludePrefab | IncludeDisabledEntities` и уничтожает его одним вызовом.

Альтернатива на будущее — отдельный `World` на ран (выгрузил мир — всё ушло). Это чище, но требует переписать `SimWorld` и все места, которые берут `World.DefaultGameObjectInjectionWorld`. Сейчас не делаем; тег даёт тот же результат дешевле.

### 3.4. Загрузка сейва без запросов

Сейчас `PlaceBuildingRequest` несёт поля, которые нужны только восстановлению: `BuildingId`, `ConstructionElapsed`, `ConstructionDuration`, `InstantComplete`, `Dismantling`, `DesiredWorkers`, `Paused`, а consumer различает их через `PlaceBuildingCommandSource { Gameplay, SnapshotRestore, Setup }`. Так разовое действие игрока и восстановление мира склеены в одну структуру, и правила валидации у них разные (оплата, unlock, проверка фазы).

Целевая форма:

- Логику размещения вынести в статический `BuildingPlacement` (Simulation/City): `TryValidate(...)` и `Spawn(...)`. Её зовут и система обработки запроса, и `ScenarioSetup`, и `SnapshotRestore`.
- `PlaceBuildingRequest` после этого содержит только то, что выбирает игрок: `TypeId`, `AnchorCluster`, `AnchorRadial`. Footprint берётся из каталога, а не из запроса.
- `SnapshotRestore.Apply` создаёт дома напрямую с сохранёнными `Construction`, `Workplace`, `BuildingId`, затем строит `NativeHashMap<int, Entity>` и создаёт агентов с уже разрешёнными ссылками (§4).
- Слайдеры пирамиды восстанавливаются прямой записью в `PyramidFeedLine`, а не `SetPyramidFeedRequest`.

### 3.5. Каталоги

- `InitializeRun` получает `DifficultyProfile` и сразу пишет **итоговые** `BuildingPrototype`, `BuildingCatalogCost`, `BuildingCatalogRecipe`.
- `RunPublisher.ApplyDifficulty` переезжает в bootstrap как шаг сборки каталога. Оверлей с bool-флагами пока остаётся (его заменит Ф5 CMS из [[05 Content Pipeline & CMS]]), но применяется в одном месте и один раз.
- `Base*`-буферы и `RebuildResolvedCatalog` удаляются.

### 3.6. Что удаляется

- `SimSessionPhase`, поле `Phase`, `AcceptsSetupCommands`, `IsReady`. Сам `SimSession` остаётся пустым маркером сессионной сущности (или заменяется `RunScoped` + конкретными синглтонами — решить по месту).
- `FinalizeSimSessionLifecycleSystem`, `SimSessionAccess.HasLifecycleQueues`, `AreLifecycleQueuesDrained`, `ResearchWorld.CommandsDrained`.
- `DespawnAllAgentsCommand`, `DespawnAllBuildingsCommand`, `ConsumeDespawnAgentsSystem`, `ConsumeDespawnBuildingsSystem`.
- `PendingScenarioPlace`, `PendingScenarioSpawns`, `ConsumePendingScenarioSpawnsSystem`.
- `PlaceBuildingCommandSource` и поля восстановления в `PlaceBuildingRequest`.
- `GameSession`: `simulationReadyTimeoutSeconds`, `WaitUntilSimulationReady`, `WaitForPhaseAsync`, `AwaitCondition`, `IsSimulationReady`, `IsSessionPhase`.
- `RunPublisher.BeginReset`, `ClearLifecycleQueues`, `RebuildResolvedCatalog`.
- `Base*`-буферы каталога.

### 3.7. Риски

- **Stories и Research** живут на тех же очередях: `StoryBake.Apply`, `StoryRestoreDialog/Quest`, `ResearchWorld.Populate/DestroyAll`. Их нужно перевести на `RunScoped` и прямое восстановление в той же фазе, иначе сломается сейв историй.
- `SimWorld` кэширует сессионную сущность. После wipe кэш должен инвалидироваться — проверка `em.Exists(_bag)` уже есть, но нужно убедиться, что новая сессия не получит тот же `Entity` с тем же версионным номером (не получит: версия растёт при уничтожении).
- Презентационные доски держат виды по `AgentId` / `BuildingId`. После wipe они должны очиститься сами по пустому запросу — проверить сценарий «выход в меню → новый ран».

### 3.8. Критерии успеха

- В `GameSession` нет ни одного `WaitUntil` по ECS и ни одного таймаута.
- «Новая игра → выход в меню → новая игра → загрузка» три раза подряд без ошибок в консоли; количество сущностей в Entities Hierarchy после выхода в меню совпадает с количеством до первого рана.
- `rg "SimSessionPhase|DespawnAll|PendingScenario|BaseBuilding"` по `Scripts` — пусто.
- Первый кадр после `Begin` уже показывает дома сценария и рабочих на плазе.

---

## 4. Ф2. Идентичность на Entity

### 4.1. Диагноз

Все связи — `int`:

| Где | Поле |
| --- | --- |
| `AgentAssignment` | `WorkplaceBuildingId`, `ConstructionBuildingId` |
| `OccupiedCell` (буфер на сессии) | `BuildingId` |
| `RoadSegment` (буфер на сессии) | `SiteId` |
| `DemolishBuildingRequest` | `BuildingId` |
| `BuildingSelection` | `SelectedBuildingId`, `HoveredBuildingId` |
| `BuildingIdTag` (на виде) | `Id` |
| `CityGrid` | `NextBuildingId` — генератор id лежит на сетке |

Следствия:

- `BuildingDismantle.TryGetBuilding` — линейный поиск дома; `Begin` и `ReleaseCrew` — по полному проходу по всем агентам.
- `AdvanceAgentCommuteSystem.TrySteerTo` и `ClaimConstructionCrewSystem.FindSite` — для каждого агента линейный поиск по списку домов: O(агенты × дома) каждый тик.
- `WorkforceDispatchSystem` и `SyncWorkplaceLoadSystem` строят `NativeHashMap<int, …>` каждый тик, чтобы обойти отсутствие прямой ссылки.
- `BuildingInspectPanel.TryFindBuilding` — `CreateEntityQuery` + два `ToArray` при каждом `Update` и при каждом клике.

### 4.2. Целевая модель

```csharp
public struct AgentAssignment : IComponentData
{
    public Entity Workplace;          // Entity.Null = свободен
    public Entity ConstructionSite;   // Entity.Null = нет задачи стройки
    public byte Arrived;
}

public struct OccupiedCell : IBufferElementData
{
    public int Cluster;
    public int Radial;
    public Entity Building;
}

public struct DemolishBuildingRequest : IComponentData
{
    public Entity Building;
}
```

`RoadSegment.SiteId` → `Entity Site` по тому же принципу.

Presentation тоже держит `Entity`: `BuildingSelection.Selected`, `BuildingIdTag.Entity`. Перед каждым чтением — `em.Exists(entity)`; это дешёвая проверка версии, а не поиск.

### 4.3. Висячие ссылки — это фича, а не баг

Когда дом уничтожен, ссылки на него у агентов становятся недействительными. Сейчас `BuildingDismantle` вручную обходит всех агентов и обнуляет id. В целевой модели **никто не бегает и не чистит**:

- Системы, которые читают ссылку, проверяют её через `ComponentLookup<T>.HasComponent(target)` (Burst-friendly, O(1)).
- Если цели нет — агент сбрасывает ссылку и возвращается в пул свободных. Это та же самоисцеляемость, что у desired state: сломанное состояние лечится следующим тиком.

```csharp
var workplaces = SystemAPI.GetComponentLookup<Workplace>(isReadOnly: true);
var transforms = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);

foreach (var (assignment, motor, pose) in SystemAPI.Query<
             RefRW<AgentAssignment>, RefRW<AgentLocomotion>, RefRO<LocalTransform>>())
{
    var target = assignment.ValueRO.Workplace;
    if (target == Entity.Null)
        continue;
    if (!workplaces.HasComponent(target))
    {
        assignment.ValueRW.Workplace = Entity.Null;
        assignment.ValueRW.Arrived = 0;
        continue;
    }
    var goal = transforms[target].Position;
    // steer
}
```

Тонкость: если дом ещё жив, но перешёл в стройку или снос, `Workplace` на нём может остаться. Проверять нужно ровно то условие, при котором работа возможна (`HasComponent<Workplace>` и отсутствие `Construction`), а не просто существование сущности.

### 4.4. `BuildingId` остаётся — но только как ключ сохранения

- `Building.Id` (или отдельный компонент `BuildingKey`) — стабильный `int` для JSON-сейва и для детерминированного порядка (§2.3).
- Генератор переезжает с `CityGrid.NextBuildingId` в отдельный синглтон `BuildingIdSequence` по образцу `AgentIdSequence`. Сетке не место для счётчика идентификаторов.
- **Правило:** в геймплейном коде поиск сущности по `BuildingId` запрещён. Разрешён только в `SnapshotRestore` (локальная таблица) и в `RunSessionSnapshot` при записи.
- `AgentId` — то же самое: ключ сейва и стабильный выбор скина Mixamo, не ссылка.

### 4.5. Что удаляется

- `BuildingDismantle.TryGetBuilding`, полные проходы по агентам в `Begin` и `ReleaseCrew`.
- `HouseRef`/`Site` списки с линейным поиском в `AdvanceAgentCommuteSystem` и `ClaimConstructionCrewSystem` (в Claim остаётся список стройплощадок для выбора ближайшей — это не поиск по id, а реальная задача).
- `NativeHashMap<int, …>` в `WorkforceDispatchSystem` и `SyncWorkplaceLoadSystem`.
- `BuildingInspectPanel.TryFindBuilding`.

### 4.6. Критерии успеха

- `rg "WorkplaceBuildingId|ConstructionBuildingId|SelectedBuildingId"` — пусто.
- `rg "\.Id != |\.Id == "` по `Simulation` — только код сейва и сортировка по ключу.
- Снос дома с назначенными рабочими: люди возвращаются на плазу в течение тика, ошибок нет.
- Сейв → загрузка: рабочие идут в те же дома, стройка продолжается с того же процента.

---

## 5. Ф3. Рабочая сила: честный desired state

### 5.1. Диагноз

```csharp
// WorkforceDispatchSystem
if (needed > 0)
{
    // No free workers available to fulfill remainder: clamp DesiredWorkers to actual assigned
    workplace.ValueRW.DesiredWorkers = current + assigned;
}
```

1. Система переписывает желание игрока. Если рабочий умрёт или людей временно не хватает, желание схлопывается до факта и уже никогда не восстановится само. Это ломает главную причину, по которой вводили паттерн.
2. Пауза отпускает людей, вопреки §2.1.
3. Проверка «есть ли свободные» продублирована в UI (`BuildingInspectPanel.CountWorkforce`: «все рабочие минус сумма желаний»), причём по другой формуле, чем в системе.
4. Нет проверки `SimControl`: система работает в меню и при выходе. Желание выравнивать на паузе можно (это удобно игроку), но не при `Mode == Off`.
5. Порядок раздачи — порядок чанков (§2.3).
6. `AssignedCount` / `WorkingCount` пересчитывает другая система (`SyncWorkplaceLoadSystem`) позже в кадре. Диспетчер считает текущее назначение сам, своим проходом. Два источника одного факта.

### 5.2. Целевая форма

Один владелец факта и одна формула:

```text
WorkforceDispatchSystem  (после CommandSystemGroup, до commute; не при Mode == Off)
  1. Пройти агентов: посчитать фактическое назначение по Entity, собрать свободных.
  2. Пройти рабочие места в стабильном порядке по ключу:
       target = min(DesiredWorkers, slots)            // Paused на штат не влияет
       если факт > target — отпустить лишних (последних назначенных)
       если факт < target — назначить из свободных, пока есть
  3. Записать Workplace.AssignedCount = факт.        // DesiredWorkers не трогать
  4. Записать сводку WorkforceSummary на сессию.

SyncWorkplaceLoadSystem
  только WorkingCount (дошли и не на стройке). Или влить в диспетчер, если удобнее.
```

```csharp
/// <summary>Read model for HUD. Written by WorkforceDispatchSystem only.</summary>
public struct WorkforceSummary : IComponentData
{
    public int TotalWorkers;
    public int IdleWorkers;
    public int DesiredTotal;
    public int UnmetDesired;   // сумма недобора по всем домам
}
```

UI читает один синглтон вместо двух запросов на кадр. Ограничение живёт в системе; UI только серит кнопку по `IdleWorkers` и показывает недобор.

Рецепт при паузе: `BuildingProductionSystem` уже пропускает `IsPaused`, менять его не нужно. После правки диспетчера UI-текст «Production stopped. Crew stays assigned.» становится правдой.

### 5.3. Критерии успеха

- `rg "DesiredWorkers\s*=" Simulation` — только восстановление сейва (если оно в Simulation) и ни одной записи в системах.
- Сценарий «желаю 5, свободных 3»: назначено 3, желание остаётся 5. Заспавнил 2 рабочих — через тик назначено 5 без действий игрока.
- Пауза: рабочие остаются назначенными, рецепт стоит, счётчик штата не меняется.
- Порядок раздачи одинаков между запусками при одинаковом сейве.

---

## 6. Ф4. Входы игрока: единые правила

### 6.1. Диагноз

- Request-сущности обрабатывают системы со старыми именами `Consume*CommandsSystem` из папок `CommandSystems`.
- В `Simulation/<Feature>/` теперь три вида папок для входов: `Commands/`, `CommandSystems/`, `Requests/`.
- `SimCommands` имеет два API: `TryPost<T>` (буфер, с fallback-поиском любой сущности с этим буфером) и `Request<T>` (сущность), плюс два специальных метода для дорог.
- Проверка «можно ли сейчас» размазана по consumer'ам через `lifecycle.IsReady` и `Source`.

### 6.2. Целевая форма

Папки внутри фичи:

```text
Simulation/<Feature>/
  Components/       состояние (включая поля желания)
  Requests/         request-компоненты
  RequestSystems/   Process*RequestsSystem
  Commands/         только буферы-потоки (часы, story UI) и outbound-события
  Systems/          тик
```

- Переименовать: `ConsumePlaceBuildingCommandsSystem` → `ProcessPlaceBuildingRequestsSystem` и т.д. Группа может остаться `CommandSystemGroup` — переименование группы не даёт ничего, кроме диффа.
- `SimCommands.TryPost<T>` — убрать fallback-поиск: буферы-потоки живут только на сессии.
- Гейт «ран идёт» — один: request-системы требуют `SimControl.SessionInGame`. После Ф1 других фаз нет, `Source` не нужен.

Честная оговорка про аллокации: `ToComponentDataArray` в request-системах **не** проблема. Запросов единицы за кадр, и копия массива нужна именно потому, что обработка делает структурные изменения (создаёт дома). Документ [[01 ECS & Commands|01]] перегибает, относя это к болезням. Проблема — в горячих системах тика и в Presentation, не здесь.

### 6.3. Слайдеры пирамиды как желание

```text
UI:      HQ entity (из CityCore, §7.2) → GetBuffer<PyramidFeedLine> → line.PerHour = value
Система: PyramidBurnSystem читает PerHour, сама отсекает недопустимое (< 0, !CanFeed, energy)
```

- Удалить `SetPyramidFeedRequest`, `ConsumeSetPyramidFeedCommandsSystem`.
- `PyramidFeed.SetPerHour` с проверкой `CanFeed` остаётся как удобная функция для UI, но **правило** (что жечь нельзя) дублируется в `PyramidBurnSystem` — запись в буфер из UI не должна быть единственной защитой.
- Задел под ритуал (не строить сейчас): когда появится `PyramidRitual`, система игнорирует изменения слайдеров на время ритуала, а UI читает флаг и серит слайдеры. Это согласуется с правилом `pyramid-timeline` про «занятый слот».

### 6.4. Критерии успеха

- В `Simulation` нет файлов `Consume*CommandsSystem`, обрабатывающих request-компоненты.
- Каждый вход в игре классифицируется по таблице §2.5 без исключений.
- Слайдер пирамиды меняет расход в том же кадре, сохраняется и загружается без запросов.

---

## 7. Ф5. Чтение в Presentation

### 7.1. Диагноз

| Где | Что каждый кадр |
| --- | --- |
| `BuildingInspectPanel.Update` | `TryFindBuilding`, `CountWorkforce` (2 запроса), `CountConstructionCrew`, `TryGetGameTime`, `FindFirstObjectByType`, форматирование строк |
| `PyramidInspectPanel.Update` | `TryFindHq` через `CreateEntityQuery` |
| `AgentViewBoard.Sync` | 5 × `ToComponentDataArray` |
| `BuildingSelection` | кэширует `EntityQuery` на MonoBehaviour (вики [[../Architecture/14 Sim Presentation Bridge|14]] это запрещает: время жизни GO ≠ время жизни World); полный проход по домам для пика по сектору |

### 7.2. Целевая форма

- **Выбор держит `Entity`** (после Ф2). Панели читают компоненты выбранной сущности напрямую.
- **Агрегаты считает симуляция.** Всё, что UI сейчас считает сам по всему миру, становится read model в симуляции:
  - `WorkforceSummary` (§5.2);
  - счётчики бригады на стройке — поля `Construction.CrewAssigned` / `CrewArrived`, пишет `ClaimConstructionCrewSystem`;
  - `CityCore { Entity Headquarters }` на сессии, пишет bootstrap. UI получает HQ за O(1) без запроса.
- **Запросы не на MonoBehaviour.** Если запрос нужен, его кэширует `SimWorld` вместе с миром (у них одно время жизни), а не виджет.
- **Ссылки на соседей** (`BuildPlacementController` для каталога видов) — через инспектор или Game-scope VContainer, не `FindFirstObjectByType` в `Update`.
- **Строки** обновлять только при изменении значения. Низкий приоритет, делать вместе с переездом на `Entity`, раз всё равно переписываем `Show`.

### 7.3. `AgentViewBoard` — сначала измерить

`Allocator.Temp` — быстрый линейный аллокатор, не GC. Пять временных массивов на сотню агентов, скорее всего, не видны в профайлере. Утверждения документа [[03 Presentation & Performance|03]] про «микрофризы» не измерены.

Порядок:

1. Профайлер на 200 агентах: время `AgentViewBoard.LateUpdate` и `GC.Alloc` в кадре.
2. Если меньше 0.2 мс и 0 B GC — не трогать, записать результат сюда.
3. Если больше — один проход по чанкам вместо пяти копий, затем думать дальше.

### 7.4. Критерии успеха

- `rg "CreateEntityQuery" Presentation/GameHud` — пусто.
- `rg "FindFirstObjectByType" -g "*.cs"` — нет вызовов внутри `Update` / `LateUpdate`.
- Открытая карточка дома и карточка пирамиды: в профайлере 0 B `GC.Alloc` от панели в кадре без изменений.

---

## 8. Ф6. Сборки, namespace, мусор

- `SimulationBootstrap`, `RunPublisher`, `RunSessionSnapshot`, `StorySave` лежат в `Presentation/Application`, но в namespace `TheyWillDescend.Shell`. Привести к `TheyWillDescend.Presentation.Application` (или к тому, что решим для Application-слоя). После Ф1 `RunPublisher` либо исчезает, либо становится `ScenarioSetup`.
- Сборка для bootstrap пока Presentation: ему нужен `BuildingCatalogAsset` из Content (префабы с `BuildingStamp`). Переезд в Simulation — после CMS-реформы, когда баланс зданий станет чистыми данными.
- Simulation не должна трогать `UnityEngine.Application` — уходит вместе с `ConsumePendingScenarioSpawnsSystem`.
- Пустые строки-заглушки и лишние `using` в затронутых файлах — по ходу, не отдельной задачей.

> **Статус: сделано, кроме пунктов, привязанных к Ф1.** Namespace Presentation приведены к правилу «сборка + первая папка» ([[../Architecture/01 Folder Structure|01]]): `Presentation/Application` → `Presentation/SimBridge` (`TheyWillDescend.Presentation.SimBridge`; имя `Application` затенило бы `UnityEngine.Application` в десяти файлах), `TheyWillDescend.Shell` → `Presentation.Shell`, `Infrastructure.Logging/Save` → `Presentation.Infrastructure`, `RTSCameraController` получил namespace `Presentation.Cameras`. Пустые строки в Presentation и Main убраны, пустые папки `Simulation/Gods/Commands` и `Simulation/Research/Commands` удалены, устаревшие вызовы API (`FindObjectsSortMode`, `FindObjectsOfType`, `FindFirstObjectByType`, `AttachInstanceToGameObject(Transform)`) заменены — консоль чистая. **Остаётся на Ф1:** `UnityEngine.Application` в Simulation (уходит вместе с `ConsumePendingScenarioSpawnsSystem`) и судьба `RunPublisher`. Simulation-файлы не трогались — фазы логики впереди.

---

## 9. Чек-лист после каждой фазы

1. Проект компилируется, консоль чистая на входе в Play.
2. Меню → новая игра → поставить дом → назначить людей → дождаться производства.
3. Снести дом с людьми и с бригадой на стройке.
4. Пауза, x1/x2/x3, Esc-меню.
5. Сохранить → выйти в меню → загрузить: дома, стройки, штат, слайдеры пирамиды, лояльность, эра, истории.
6. Выйти в меню → новая игра (без перезапуска Play).
7. Коммит одной фазы.

---

## 10. Ф7. Синхронизация вики и правил

Делать в конце каждой фазы, а не одним рывком в конце.

| Документ | Что исправить |
| --- | --- |
| [[../Architecture/01 Folder Structure|01 Folder Structure]] | Папки Authoring (Session/Agents/City/Economy больше нет), структура `Requests/RequestSystems`, pipeline `CommandSystemGroup` |
| [[../Architecture/02 Scenes & Lifetime|02 Scenes]] | Нет SubScene в Game; `GameSession` в Game, не в Root |
| [[../Architecture/03 Core Systems|03 Core Systems]] | Нет AppFlow / `LoadingGame`; поток старта рана по §3.2 |
| [[../Architecture/04 Simulation|04 Simulation]] | Нет assign/unassign/pause-команд; новый pipeline тика |
| [[../Architecture/14 Sim Presentation Bridge|14 Bridge]] | Нет `SimControlAuthoring`, фаз и despawn; таблица входов §2.5 |
| [[../Architecture/15 Refactoring — Target ECS & Interaction|15]] | Семантика паузы по §2.1; пример `WorkforceDispatchSystem` не пишет желание |
| [[../Architecture/Index|Architecture Index]] | ~~Убрать ссылку на несуществующий `10 Vertical Slice`~~ — сделано вместе с 08 Ф6 |
| `Refactoring/01–06` | Ссылки `file:///f:/...` заменить на вики-ссылки или пути от `Assets/`; пометить выполненные пункты |
| `.cursor/rules/ecs-production-architecture.mdc` | Папки фичи по §6.2, таблица входов §2.5, правило «система не пишет поле желания» |

---

## 11. Вне рамок этих правок

- **CMS** (`BuildingDefinition`, `GameDatabase`, множители сложности) — [[05 Content Pipeline & CMS]]. Начинать после Ф1–Ф4: bootstrap к тому моменту будет одним местом сборки каталога, и замена источника данных станет локальной.
- **Stories.** `AdvanceStorySystem` — 695 строк, около 33 созданий запросов, буферы на сессии. В Ф1 трогаем только жизненный цикл (wipe, прямое восстановление). Отдельный аудит — после фундамента.
- **Pathfinding, нужды, законы, кризисы** — по GDD, после того как экономический цикл стоит на этом фундаменте.

---

## 12. Контрольные вопросы

1. Почему `WorkforceDispatchSystem` не имеет права писать `DesiredWorkers`, даже «для удобства UI»? Что сломается при смерти рабочего?
2. Почему висячая `Entity`-ссылка у агента лучше, чем ручная очистка `int Id` при сносе?
3. Чем request-сущность отличается от буфера на сессии, и почему часы остаются буфером?
4. Почему нельзя чистить ран через `DestroyEntity(UniversalQuery)`?
5. Почему загрузка сейва не должна идти через `PlaceBuildingRequest`?
6. Где должна жить проверка «нельзя назначить больше людей, чем свободно», и что тогда делает кнопка «+»?
7. Почему `ToComponentDataArray` в системе обработки запросов допустим, а в `AgentViewBoard` его стоит измерить?

---

Связанные: [[Index|План рефакторинга]] · [[04 Step-by-Step Roadmap]] · [[../Architecture/14 Sim Presentation Bridge|14 Bridge]] · [[../Architecture/15 Refactoring — Target ECS & Interaction|15 Target ECS]]
