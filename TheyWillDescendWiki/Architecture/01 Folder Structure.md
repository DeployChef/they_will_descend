# 01 Folder Structure

← [[00 Overview]] | [[Index]] | Далее → [[02 Scenes & Lifetime]]

## Закон

**Сборка = стена компилятора** (кто не видит кого, что не попадёт в билд).  
**Папка внутри = фича, затем слой.** Не зеркало Clean Architecture и не туториальный «все компоненты отдельно от всех систем».

Новая asmdef оправдана, только если без неё либо запретная ссылка компилируется, либо в билд едет код, которого там быть не должно.

```
Authoring  →  Simulation
Presentation  →  Simulation
Main  →  Presentation
```

`TheyWillDescend.Simulation` **не** ссылается на Presentation, uGUI, Input System, Animator.  
`TheyWillDescend.Authoring` **не** ссылается на Presentation.  
Simulation и Authoring: `autoReferenced: false` — на них ссылаются явно.

## Сборки

| Asmdef | Что внутри | Нельзя |
| --- | --- | --- |
| `TheyWillDescend.Simulation` | компоненты, команды, системы, сетка, occupancy | UI, виды, диск, FMOD |
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
    Content/     каталоги (плоские SO-типы)
  Authoring/
    Session/     SimControlAuthoring
    Agents/      AgentSessionAuthoring
    City/        CityGrid, Building, catalog, HQ
    Economy/     ResourceCatalogAuthoring
    Time/        GameTimeAuthoring
    Scenario/    ScenarioAuthoring + bake
    Editor/      Scene tools сценария
  Presentation/
    Agents/      AgentSpawner, AgentViewBoard
    City/        placement, grid guide, BuildingViewBoard, BuildingSelection, BuildingRejectLog
    GameHud/     Time / Resource / Build / Inspect / Save / Spawn
    Shell/       FSM, GameSession, SceneLoader, GameInput
    ShellUi/     PressAnyKeyScreen, MainMenuScreen
    Application/ RunSessionSnapshot
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

`CommandSystemGroup` живёт в `Session/CommandSystems`. Namespace остаётся на уровне фичи (`TheyWillDescend.Simulation.City`), не `City.Commands`.

Папка `Simulation/Io` — **не канон** (старый дубль). Порт: `Session/Components/SimWorld`, `SimBridge`, `SimBridgeAccess`; `Session/Commands/SimCommands`.

Новая механика: система в `Simulation/<фича>/Systems`, команда рядом в `Commands`, consume в `CommandSystems`, вид в `Presentation/<фича>`.  
Одна команда / один флаг сноса — **своя система** в `CommandSystemGroup`. Не общий `SimCommandProcessor`. HUD только `SimCommands.TryPost<T>`.

`GameHud` — оверлей-виджеты. `*View` / `*ViewBoard` — меш в мире следует за entity. Меню: экраны на сцене регистрируют `.Current` в Awake; стейты читают их в Enter.

Связанные: [[00 Overview]] · [[08 Production ECS]] · [[09 App Shell]] · [[14 Sim Presentation Bridge]]
