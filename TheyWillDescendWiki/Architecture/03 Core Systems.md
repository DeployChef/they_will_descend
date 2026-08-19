# 03 Core Systems

← [[02 Scenes & Lifetime]] | [[Index]] | Далее → [[04 Simulation]]

Разделение: **Shell-сервисы** vs **Simulation (ECS)**. Общая картина оболочки — [[09 App Shell]].

## Shell (вне ECS)

| Служба | Ответственность |
| --- | --- |
| **AppFlow / AppStateMachine** | FSM продукта: стейты Enter/Exit, переходы |
| **GameSession** | Start/Dispose одной попытки сценария |
| **SimGate** | Off / Running / Frozen + Speed (x1/x2/x3) — мост к ECS-тикам |
| **SceneLoader** | узкая загрузка сцен (не бог) |
| **Shell Event Bus** | UI/audio/flow (позже) |
| **Audio / FMOD** | на Root, переживает выгрузку Game |
| **GameLog** | каналы логов (`Presentation/Infrastructure/Logging`) |

Composition Root без DI на старте — сборка `TheyWillDescend.Main`. Код Shell живёт в Presentation.

## Simulation (ECS)

| Домен | Ответственность |
| --- | --- |
| **Time** | singleton `GameTime` + часы/скорость (`SimClock`); тик только при Running; HUD читает проекцию суток. Save/load: [[13 Time HUD and Save]] |
| Economy / Workforce / … | см. [[04 Simulation]] · [[08 Production ECS]] |
| Domain events | ECS buffers → Presentation drain (`AgentViewBoard`) |

Игровое время **не** живёт в VContainer-сервисе. Истина — компонент в World.

## Что уже есть в коде

| Артефакт | Путь |
| --- | --- |
| `GameTime` | `Scripts/Simulation/Time/GameTime.cs` |
| `AdvanceGameTimeSystem` | `Scripts/Simulation/Time/AdvanceGameTimeSystem.cs` |
| `GameTimeAuthoring` | `Scripts/Authoring/Time/GameTimeAuthoring.cs` |
| `GameLog` | `Scripts/Presentation/Infrastructure/Logging/` |

## Порядок ответственности при старте рана

```
AppFlow → Briefing «Начать»
  → Director готов Game + SessionConfig
  → CameraDirector (подлёт)
  → SimGate.Running
  → ECS Time/Economy тикают
```

---

Связанные: [[09 App Shell]] · [[04 Simulation]] · [[../GDD/02 Gameplay Loop|Gameplay Loop]]
