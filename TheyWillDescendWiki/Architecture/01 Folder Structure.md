# 01 Folder Structure

← [[00 Overview]] | [[Index]] | Далее → [[02 Scenes & Lifetime]]

## Закон

**Сборка = стена компилятора** (кто не видит кого, что не попадёт в билд).  
**Папка внутри = фича, затем слой.** Не зеркало Clean Architecture и не туториальный «все компоненты отдельно от всех систем».

Новая asmdef оправдана, только если без неё либо запретная ссылка компилируется, либо в билд едет код, которого там быть не должно.

```
Authoring      →  Simulation, Content
Presentation   →  Simulation, Content
Content        →  Simulation
Main           →  Presentation, Input System (ассет .inputactions для InputService)
```

`TheyWillDescend.Simulation` **не** ссылается на Content, Presentation, uGUI, Input System, Animator.  
`TheyWillDescend.Authoring` **не** ссылается на Presentation.  
`TheyWillDescend.Content` — арт-реестры (`typeId` → префаб). Не логический снимок рана.  
Simulation, Authoring, Content: `autoReferenced: false` — на них ссылаются явно.

## Сборки

| Asmdef | Что внутри | Нельзя |
| --- | --- | --- |
| `TheyWillDescend.Simulation` | компоненты, команды, системы, сетка, occupancy; логические SO (`ResourceDefinition`, `SimRules`) | UI, виды, префабы домов, диск, FMOD |
| `TheyWillDescend.Content` | каталог зданий (`BuildingCatalogAsset`), наборы облика (`VisualSet`) | `ISystem`, экономика рана |
| `TheyWillDescend.Authoring` | Baker’ы SubScene, editor-tools сценария | runtime UI |
| `TheyWillDescend.Presentation` | HUD, ghost, view boards, Shell-сервисы, мост к симуляции, JSON-сейв, `GameLog`, `AudioService` | `ISystem` / `SystemBase` на виджеты; писать стоки, лояльность, позу |
| `TheyWillDescend.Main` | `AppScope` (процесс), `MainMenuLifetimeScope`, `GameLifetimeScope`. Ссылается на Presentation, Content, Input System | экономика, `EntityManager` |

Домены Shell / SimBridge / Infrastructure — **папки** в Presentation, не отдельные сборки.

**Namespace = сборка + первая папка:** `TheyWillDescend.Presentation.<Папка>`, `TheyWillDescend.Simulation.<Фича>`. Подпапки внутри (`Shell/Scenes`, `Audio/Service`, `City/Components`) namespace не дробят.  
Имена `Application` и `Camera` для namespace не используем: внутри `TheyWillDescend.*` они затеняют `UnityEngine.Application` и `UnityEngine.Camera`. Поэтому `Presentation/SimBridge` и `Presentation.Cameras`.

Позже, когда заболит: Editor-tooling, dedicated server, чужие SDK (Steam) — тогда новая стена.

## Папки (канон)

```
Assets/_Project/Scripts/
  Simulation/
    Session/     Components, Commands, CommandSystems   ← часы, SimWorld, SimCommands
    Time/        Components, Systems
    City/        Components, Commands, Requests, RequestSystems, Systems, Math
    Agents/      Components, Requests, RequestSystems, Systems
    Economy/     Components, Systems
    Gods/        Components, Requests, RequestSystems, Systems
    Research/    Components, Requests, RequestSystems, Systems
    Content/     каталоги логики (ResourceDefinition, SimRules, Timeline, BuildingStamp identity)
  Content/       BuildingCatalogAsset (оболочки зданий со штампом), VisualSet (модели), DifficultyProfile (оверлей), ScenarioDefinition
  Authoring/
    Session/     SimControlAuthoring, SimRulesAuthoring
    Agents/      AgentSessionAuthoring
    City/        CityGrid, Building, catalog, HQ
    Economy/     ResourceCatalogAuthoring
    Scenario/    ScenarioAuthoring + bake
    Editor/      Scene tools сценария
  Presentation/
    Agents/      AgentSpawner, AgentViewBoard
    City/        placement, grid guide, BuildingViewBoard, BuildingSelection, BuildingRejectLog
    GameHud/     Time / TimelineRibbon / Resource / Build / Inspect / Save / Spawn
    Camera/      RTSCameraController, PauseCamera*            (namespace Presentation.Cameras)
    SimBridge/   SimulationHost, SimulationBootstrap, RunSessionSnapshot, SnapshotRestore, ScenarioSetup, SimPauseAudioBridge
    Shell/       AppContext, Scenes (ShellService, GameScenes),
                 Session (GameEntry, RunLaunch), Input (InputService)
    ShellUi/     MainMenu, Game (PauseMenuScreen), Loading (LoadingScreen)
    Infrastructure/ Logging (GameLog), Save (SaveService, RunSnapshot)
    Audio/       звук мира (ячейки, ветер, стройка); Service/ — AudioService, каталог, банки, музыка
    Environment/ SceneDressing, TimeLightController
  Main/          AppScope, MainMenuLifetimeScope, GameLifetimeScope
```

Ассеты вне `Scripts/`:

```
Assets/_Project/
  Content/Buildings/   каталог и оболочки зданий (штамп, вид, виджет, звук — без модели)
  Content/Visuals/     FullVisuals.asset, Bodies/ — модели зданий полной сцены
  Prefabs/Game/        GameRun.prefab — весь функционал рана, общий для сцен рана
  Scenes/              MainMenu, Game
  Blockout/            GameBlockout.unity, BlockoutVisuals, блоки, материалы. Game на эту папку не ссылается
```

Внутри фичи Simulation:

| Подпапка | Что |
| --- | --- |
| `Components/` | состояние, поля желания |
| `Requests/` | одноразовые request-сущности |
| `RequestSystems/` | `Process*RequestsSystem` в `CommandSystemGroup` |
| `Commands/` | буферы-потоки (`SimClockCommand`) и outbound (`BuildingRejectedEvent`) |
| `CommandSystems/` | только буферные consume; группа и часы живут в `Session/CommandSystems` |
| `Systems/` | тик (commute, construction, produce) |

`CommandSystemGroup` живёт в `Session/CommandSystems`. Pipeline: clock → spawn → place → road stroke → demolish road → demolish building → pyramid feed → research → delta time. Штат и пауза дома в очередь не входят: UI пишет `Workplace`, диспетчер выравнивает факт. Namespace на уровне фичи (`TheyWillDescend.Simulation.City`), не `City.Requests`.

Папка `Simulation/Io` — **не канон** (старый дубль). Порт: `Session/Components/SimWorld`, `SimSession`, `SimSessionAccess`; `Session/Commands/SimCommands`.

Новая механика: система в `Simulation/<фича>/Systems`, вид в `Presentation/<фича>`. Желание — поле на компоненте. Разовое действие — `Requests/` + `Process*RequestsSystem`. Поток за кадр — буфер в `Commands/`.  
Один вид входа — **своя система** в `CommandSystemGroup`. Не общий процессор. Presentation не вызывает process-системы вручную.

`GameHud` — оверлей-виджеты. `*View` / `*ViewBoard` — меш в мире следует за entity. Меню: экраны на сцене регистрируют `.Current` в Awake; стейты читают их в Enter.

Связанные: [[00 Overview]] · [[08 Production ECS]] · [[09 App Shell]] · [[14 Sim Presentation Bridge]]
