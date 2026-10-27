# 03 Core Systems

← [[02 Scenes & Lifetime]] | [[Index]] | Далее → [[04 Simulation]]

Разделение: **Shell** vs **Simulation (ECS)**. Оболочка: [[09 App Shell]].

## Shell (вне ECS)

| Служба | Ответственность |
| --- | --- |
| Служба | Срок жизни | Ответственность |
| --- | --- | --- |
| **ShellService** | процесс | навигация: `StartRun` / `RestartRun` / `GoToMenu`, параметры запуска. **Нет** верхней FSM |
| **TransitionService** | процесс | единственный загрузчик сцен: экран → teardown → Single-загрузка → `LoadingTasks` → экран прочь |
| **LoadingTasks** | процесс | сцена кладёт сюда работу, которую надо дождаться под экраном загрузки |
| **InputService** | процесс | клон `.inputactions`; Menu/Proceed, Game/Pause (Esc) |
| **AudioService** | процесс | банки по наборам, музыка, снапшоты, UI-звук. ECS не читает |
| **SaveService** | процесс | слот сейва |
| **GameEntry** | сцена Game | выбирает запуск, кладёт подготовку рана в `LoadingTasks`, на Ready включает часы, ввод, меню паузы |
| **GameSession** | сцена Game | `Begin` / `Apply` / `Shutdown` одной попытки; ждёт bake через `SimWorld.TryGet` |
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
MainMenu «Начать» → ShellService.StartRun(launch)
  → TransitionService: экран загрузки, Single-загрузка Game
  → GameEntry: банки GameWorld + GameSession.Begin(launch) в LoadingTasks
  → Ready: GameEntry.Arm → SimClockCommand.InGame(true), карта Game, меню паузы
  → ECS Time / City / Agents / Economy тикают, пока Mode == Running
```

---

Связанные: [[09 App Shell]] · [[04 Simulation]] · [[../GDD/02 Gameplay Loop|Gameplay Loop]]
