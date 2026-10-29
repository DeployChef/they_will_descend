# 03 Core Systems

← [[02 Scenes & Lifetime]] | [[Index]] | Далее → [[04 Simulation]]

Разделение: **Shell** vs **Simulation (ECS)**. Оболочка: [[09 App Shell]].

## Shell (вне ECS)

| Служба | Срок жизни | Ответственность |
| --- | --- | --- |
| **AppContext** | процесс | `CurrentLaunch`, `SplashShown`. Не статика, чтение билет не стирает |
| **ShellService** | процесс | `StartRun` / `RestartRun` / `GoToMenu` и загрузка сцены: экран → Single → `LoadingScreen.Wait` → экран прочь. **Нет** верхней FSM, ран не знает |
| **LoadingScreen** | процесс | шторка и список работы сцены: `Add(task, weight, label)`. Упавшая задача бросает исключение |
| **InputService** | процесс | клон `.inputactions`; Menu/Proceed, Game/Pause (Esc) |
| **AudioService** | процесс | банки по наборам, музыка, снапшоты, UI-звук. ECS не читает |
| **SaveService** | процесс | слот сейва |
| **GameEntry** | сцена Game | выбирает запуск, кладёт `SimulationHost.Open` на `LoadingScreen`, включает ввод, меню паузы, музыку |
| **SimulationHost** | сцена Game | `Open` / `Dispose` одной партии: wipe, bootstrap, сценарий или сейв; на смерти сцены снова wipe |
| **SimPauseAudioBridge** | сцена Game | читает `SimControl.PlayerPaused`, передаёт паузу в `AudioService` |
| **GameLog** | статика | каналы логов (`Presentation/Infrastructure/Logging`) |

Composition Root — сборка `TheyWillDescend.Main`: `AppScope` (процесс) и scope'ы сцен. Код Shell живёт в Presentation. Подробно: [[09 App Shell]].

Меню-экраны: `PressAnyKeyScreen.Current` / `MainMenuScreen.Current`. Нет `ShellUiBinder` / `IShellUi` / порта.

## Simulation (ECS)

| Домен | Ответственность |
| --- | --- |
| **Time / Session** | `SimControl` (Mode, Speed, DeltaTime, замки) + `GameTime` (сутки). HUD читает. Save/load: [[13 Time HUD and Save]] |
| City / Agents / Economy | см. [[04 Simulation]] · [[08 Production ECS]] |
| Reject наружу | `BuildingRejectedEvent` → `BuildingRejectLog` |

Игровое время **не** живёт в C#-сервисе. Истина — компоненты на session entity.

`DeltaTime` = кадр × Speed. **Не** обнуляется на паузе: системы сами выходят, если `Mode != Running`.

## Что уже есть в коде

| Артефакт | Путь |
| --- | --- |
| `SimControl` | `Scripts/Simulation/Session/Components/SimControl.cs` |
| `SimClockCommand` | `Scripts/Simulation/Session/Commands/SimClockCommand.cs` |
| `SimCommands` | `Scripts/Simulation/Session/Commands/SimCommands.cs` |
| `GameTime` | `Scripts/Simulation/Time/Components/` |
| `AdvanceGameTimeSystem` | `Scripts/Simulation/Time/Systems/` |
| `GameLog` | `Scripts/Presentation/Infrastructure/Logging/` |

## Порядок при старте рана

```
MainMenu «Начать» → ShellService.StartRun(launch) пишет AppContext.CurrentLaunch
  → ShellService: экран загрузки, Single-загрузка Game
  → GameEntry читает CurrentLaunch: банки GameWorld + SimulationHost.Open(launch) на LoadingScreen
  → новая партия уже Running на скорости 1; сейв восстановил паузу и скорость
  → GameEntry включает карту Game, меню паузы, музыку Gameplay
  → ECS Time / City / Agents / Economy тикают, пока Mode == Running
```

---

Связанные: [[09 App Shell]] · [[04 Simulation]] · [[../GDD/02 Gameplay Loop|Gameplay Loop]]
