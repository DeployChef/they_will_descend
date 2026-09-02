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
Main           →  Presentation
```

`TheyWillDescend.Simulation` **не** ссылается на Content, Presentation, uGUI, Input System, Animator.  
`TheyWillDescend.Authoring` **не** ссылается на Presentation.  
`TheyWillDescend.Content` — арт-реестры (`typeId` → префаб). Не логический снимок рана.  
Simulation, Authoring, Content: `autoReferenced: false` — на них ссылаются явно.

## Сборки

| Asmdef | Что внутри | Нельзя |
| --- | --- | --- |
| `TheyWillDescend.Simulation` | компоненты, команды, системы, сетка, occupancy; логические SO (`ResourceDefinition`, `SimRules`) | UI, виды, префабы домов, диск, FMOD |
| `TheyWillDescend.Content` | арт-каталоги (`BuildingCatalogAsset`) | `ISystem`, экономика рана |
| `TheyWillDescend.Authoring` | Baker’ы SubScene, editor-tools сценария | runtime UI |
| `TheyWillDescend.Presentation` | HUD, ghost, view boards, Shell FSM, JSON-сейв, `GameLog`, FMOD-хост | `ISystem` / `SystemBase` на виджеты; писать стоки/occupy в обход команд |
| `TheyWillDescend.Main` | `Startup`, `AppFlowFactory` — вход и регистрация | экономика, `EntityManager` |

Домены Shell / Application / Infrastructure — **папки** в Presentation, не отдельные сборки.  
`TheyWillDescend.App` как имя сборки не используем: внутри `TheyWillDescend.*` оно затеняет `UnityEngine.Application`.

Позже, когда заболит: Editor-tooling, dedicated server, чужие SDK (Steam) — тогда новая стена.

## Папки (канон)

```
Assets/_Project/Scripts/
  Simulation/
    Session/     Components, Commands, CommandSystems   ← часы, SimWorld, SimCommands
    Time/        Components, Systems
    City/        Components, Commands, CommandSystems, Systems, Math
    Agents/      Components, Commands, CommandSystems, Systems
    Economy/     Components, Systems
    Gods/        Components, Commands, CommandSystems, Systems
    Content/     каталоги логики (ResourceDefinition, SimRules, Timeline, BuildingStamp identity)
  Content/       BuildingCatalogAsset (арт+штамп), DifficultyProfile (оверлей), ScenarioDefinition
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
    Application/ RunPublisher, RunSessionSnapshot
    Shell/       FSM, GameSession, SceneLoader, GameInput
    Infrastructure/ Logging, Save
    Audio/       GameAudio
  Main/          Startup, AppFlowFactory
```

Внутри фичи Simulation:

| Подпапка | Что |
| --- | --- |
| `Components/` | состояние |
| `Commands/` | буферы намерений / reject-события |
| `CommandSystems/` | consume команд (`CommandSystemGroup`) |
| `Systems/` | тик (commute, construction, produce) |

`CommandSystemGroup` живёт в `Session/CommandSystems`. Его pipeline линеен: clock → reset agents → reset buildings → scenario spawn → spawn → place → assign → unassign → workplace pause → pyramid feed → lifecycle finalizer → delta time. Finalizer переводит `SimSession.Phase` только после опустошения всех входящих очередей. Namespace остаётся на уровне фичи (`TheyWillDescend.Simulation.City`), не `City.Commands`.

Папка `Simulation/Io` — **не канон** (старый дубль). Порт: `Session/Components/SimWorld`, `SimSession`, `SimSessionAccess`; `Session/Commands/SimCommands`.

Новая механика: система в `Simulation/<фича>/Systems`, команда рядом в `Commands`, consume в `CommandSystems`, вид в `Presentation/<фича>`.  
Одна команда / один флаг сноса — **своя система** в `CommandSystemGroup`. Не общий `SimCommandProcessor`. HUD только `SimCommands.TryPost<T>`. Presentation не вызывает consume-системы вручную: `GameSession` асинхронно ждёт ECS-фазы `Ready` / `Unprepared`.

`GameHud` — оверлей-виджеты. `*View` / `*ViewBoard` — меш в мире следует за entity. Меню: экраны на сцене регистрируют `.Current` в Awake; стейты читают их в Enter.

Связанные: [[00 Overview]] · [[08 Production ECS]] · [[09 App Shell]] · [[14 Sim Presentation Bridge]]
