# 14 Sim ↔ Presentation Bridge

← [[13 Time HUD and Save]] | [[Index]] | [[../Home|Home]]

Закон:

> ECS хранит мир (векторы, id, правила). Presentation хранит виды (Transform, Animator, префаб).  
> UI меняет мир **командой**. UI узнаёт о фактах **событием** или **pull** числа.

Не кладём в entity: `Transform`, `Animator`, `GameObject`.  
Ключ вида (`AgentVisualId` = имя префаба в каталоге) — да: это факт рана для сейва, не ссылка на ассет.

## Потоки

```text
HUD / load
  → SimIo (SpawnAgentCommand / DespawnAll)
  → CommandSystemGroup → AgentCommandProcessor
  → entity: AgentId + AgentVisualId + AgentPosition + CircleWalk
  → буфер AgentSpawnedEvent / AgentDespawnedEvent / DayChangedEvent

AgentViewBoard (LateUpdate)
  ← drain events: Instantiate / DestroyImmediate вида
  ← pull AgentPosition → Transform
  ← SimGate → Animator.speed

HUD часов
  ← pull GameTime (каждый кадр)
```

`AgentId` — ключ моста на ран, не `Entity(53,1)` и не имя меша. `AgentVisualId` — ключ каталога; `AgentViewBoard` ищет префаб, Random только если ключ пустой/неизвестный.

## Что где

| Кусок | Слой |
| --- | --- |
| `SpawnAgentCommand`, `SimIo`, `AgentCommandProcessor` | Simulation / тонкий край для UI |
| `AgentSpawnedEvent`, `DayChangedEvent` | Simulation (факты за тик) |
| `AgentPosition`, `AgentId`, `AgentVisualId`, `CircleWalk` | Simulation |
| `AgentViewBoard`, `AgentView`, `AgentSpawner` (intent) | Presentation |
| Пауза / x1 x2 x3 | Shell `SimGate` — не команда ECS |

Стройка домов пока всё ещё Presentation (occupy). Когда occupancy уйдёт в ECS — тот же паттерн: команда `PlaceBuilding`, event `BuildingPlaced`, вид дома снаружи.

Связанные: [[08 Production ECS]] · [[10 Vertical Slice — Shell + ECS Walkers]] · [[13 Time HUD and Save]]
