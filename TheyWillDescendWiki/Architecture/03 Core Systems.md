# 03 Core Systems

← [[02 Scenes & Lifetime]] | [[Index]] | Далее → [[04 Simulation]]

Разделение: **Shell** vs **Simulation (ECS)**. Оболочка: [[09 App Shell]].

## Shell (вне ECS)

| Служба | Ответственность |
| --- | --- |
| **AppStateMachine** | FSM продукта: Enter/Exit, `TransitionTo`. **Нет Tick** |
| **GameSession** | `StartAsync` / `DisposeAsync` одной попытки; ждёт bake через `SimWorld.TryGet` |
| **SceneLoader** | узкая загрузка сцен (UniTask) |
| **GameInput** | клон `.inputactions`; Menu/Proceed, Game/Pause (Esc) |
| **GameAudio** | FMOD на Bootstrap; пауза музыки читает `SimControl.PlayerPaused` |
| **GameLog** | каналы логов (`Presentation/Infrastructure/Logging`) |

Composition Root — сборка `TheyWillDescend.Main`. `Startup` **не** имеет `Update`. Код Shell живёт в Presentation.

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
AppFlow → MainMenu «Начать»
  → LoadingGame: session.StartAsync (Loading + Game, unload MainMenu)
  → Playing: SimClockCommand.InGame(true)
  → ECS Time / City / Agents / Economy тикают, пока Mode == Running
```

---

Связанные: [[09 App Shell]] · [[04 Simulation]] · [[../GDD/02 Gameplay Loop|Gameplay Loop]]
