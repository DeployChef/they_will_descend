# 14 Sim ↔ Presentation Bridge

← [[13 Time HUD and Save]] | [[Index]] | [[../Home|Home]]

Закон:

> Поза в ECS — `LocalTransform`. Дом — испечённый entity-штамп, рисует Entities Graphics.  
> Человек: `Instantiate(SimPrototypes.Agent)` + `AgentType`; Mixamo GO копирует позу, пока жив Animator.

Не кладём в entity: `Transform`, `Animator`, `GameObject`, слот Mixamo.  
Тип агента (`AgentType`) — да. Какой FBX — таблица Presentation (`AgentKind` → префабы, скин стабилен от `AgentId`).

## Потоки

```text
кнопка
  → SimIo.TryEnqueue… (буфер на испечённом session entity)
  → CommandSystemGroup (тик, не Flush из UI)
  → Instantiate(SimPrototypes.Agent / House*) + LocalTransform

вид (LateUpdate)
  ← query какие entity есть
  ← LocalTransform → Transform (только Animator-люди и зона дома)

HUD часов
  ← pull GameTime

load
  → PlaybackCommands() один раз, чтобы слот применился в этом кадре
```

Отказ стройки — `BuildingRejectedEvent` (тост). Спавн/день — не события.

## Что где

| Кусок | Слой |
| --- | --- |
| `SpawnAgentCommand`, `PlaceBuildingCommand`, `SimIo` | Simulation |
| `LocalTransform`, `CircleWalk`, `Building`, `OccupiedCell` | Simulation |
| Session singleton (Baker `SimControlAuthoring`) | SubScene |
| `AgentViewBoard` / `BuildingViewBoard` | Presentation, pull |
| Пауза / x1 x2 x3 | `SimGate` (Presentation/Shell) → `SimControl.DeltaTime` |

Связанные: [[08 Production ECS]] · [[10 Vertical Slice — Shell + ECS Walkers]] · [[13 Time HUD and Save]]
