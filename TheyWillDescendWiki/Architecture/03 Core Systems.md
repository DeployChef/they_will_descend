# 03 Core Systems

← [[02 Scenes & Lifetime]] | [[Index]] | Далее → [[04 Simulation]]

Разделение: **Shell-сервисы** vs **Simulation (ECS)**. Общая картина оболочки — [[09 App Shell]].

## Shell (вне ECS)

| Служба | Ответственность |
| --- | --- |
| **AppFlow** | FSM продукта: Boot → Menu → Cutscene → Briefing → Playing ⇄ Paused → Results |
| **GameDirector** | load/unload Game, Build/Dispose child scope, SessionConfig |
| **SimGate** | Off / Running / Frozen — единственный мост «тикает ли город» |
| **Shell Event Bus** | UI/audio/flow события (не write model города) |
| **Audio / FMOD** | на Root, переживает выгрузку Game |
| **GameLog** | каналы логов (`Infrastructure/Logging`) |

Взято из gmtk по форме: Director + Root scope + audio + pause keys.  
Расширено до полного AppFlow и **SimGate под ECS**.

## Simulation (ECS)

| Домен | Ответственность |
| --- | --- |
| **Time** | singleton `GameTime`, `AdvanceGameTimeSystem` (только при SimGate.Running) |
| Economy / Workforce / … | см. [[04 Simulation]] · [[08 Production ECS]] |
| Domain events | ECS buffers → Presentation bridge → Shell bus (по мере надобности) |

Игровое время **не** живёт в VContainer-сервисе. Истина — компонент в World.

## Что уже есть в коде

| Артефакт | Путь |
| --- | --- |
| `GameTime` | `Scripts/Simulation/Time/GameTime.cs` |
| `AdvanceGameTimeSystem` | `Scripts/Simulation/Time/AdvanceGameTimeSystem.cs` |
| `GameTimeAuthoring` | `Scripts/Authoring/Time/GameTimeAuthoring.cs` |
| `GameLog` / `LogChannel` | `Scripts/Infrastructure/Logging/` |

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
