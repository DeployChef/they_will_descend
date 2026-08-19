# 14 Sim ↔ Presentation Bridge

← [[13 Time HUD and Save]] | [[Index]] | [[../Home|Home]]

## Закон (однонаправленно)

World — единственный write model игры. Стрелки не симметричны:

```text
Presentation  →  команды (`SimIo`)     единственная запись из UI
Presentation  ←  query / pull          единственное чтение в вид
Simulation    ✕  TimeWidget / ViewBoard / Animator
```

`SystemBase` в Presentation, который вызывает `TimeWidget.ShowTime` или `*ViewBoard.Sync`, — это **обратная** связь: ECS-тик владеет видом. Так нельзя. Канон: виджет/доска в `Update` / `LateUpdate` сами читают World (`using var query`). Query не кэшировать на MonoBehaviour (lifetime GO ≠ lifetime World).

Редкое исключение наружу: `BuildingRejectedEvent` (тост). Не спавн, не тик дня.

Пауза/скорость — не запись игрового состояния: `SimGate` → `SimControl.DeltaTime`.

## Меши и поза

> Поза в ECS — `LocalTransform`. Дом — испечённый entity-штамп, рисует Entities Graphics.  
> Человек: `Instantiate(SimPrototypes.Agent)` + `AgentType`; Mixamo GO копирует позу, пока жив Animator.

Не кладём в entity: `Transform`, `Animator`, `GameObject`, слот Mixamo.  
Тип агента (`AgentType`) — да. Какой FBX — таблица Presentation (`AgentKind` → префабы, скин стабилен от `AgentId`).

## Потоки

```text
кнопка spawn / place
  → SimIo.TryEnqueue… (буфер на session)
  → CommandSystemGroup (тик, не Flush из UI)
  → агент: Instantiate(SimPrototypes.Agent)
  → дом: Building + Construction + LocalTransform (меша нет)
       complete → Instantiate(House)

вид (LateUpdate pull)
  ← query какие entity есть
  ← стройка: обводка + бар (pull Construction)
  ← готовый дом: обводка; меш рисует Entities Graphics
  ← люди: LocalTransform → Mixamo Transform
```

Отказ стройки — `BuildingRejectedEvent` (тост). Спавн/день — не события.

## Что где

| Кусок | Слой |
| --- | --- |
| `SpawnAgentCommand`, `PlaceBuildingCommand`, `SimIo` | Simulation |
| `LocalTransform`, `CircleWalk`, `Building`, `OccupiedCell` | Simulation |
| Session singleton (Baker `SimControlAuthoring`) | SubScene |
| `AgentViewBoard` / `BuildingViewBoard` / `TimeWidget` | Presentation **читает** World. Системы сима вью не знают. |
| Пауза / x1 x2 x3 | `SimGate` (Presentation/Shell) → `SimControl.DeltaTime` |

Связанные: [[08 Production ECS]] · [[10 Vertical Slice — Shell + ECS Walkers]] · [[13 Time HUD and Save]]
