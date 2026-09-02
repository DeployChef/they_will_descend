# 14 Sim ↔ Presentation Bridge

← [[13 Time HUD and Save]] | [[Index]] | [[../Home|Home]]

## Закон (однонаправленно)

World — единственный write model игры. Стрелки не симметричны:

```text
Presentation  →  SimCommands.TryPost     единственная запись из gameplay UI
Presentation  ←  query / pull            единственное чтение в вид
Simulation    ✕  TimeWidget / ViewBoard / Animator
```

`SystemBase` в Presentation, который вызывает `TimeWidget.ShowTime` или `*ViewBoard.Sync`, — это **обратная** связь: ECS-тик владеет видом. Так нельзя. Канон: виджет/доска в `Update` / `LateUpdate` сами читают World. Query не кэшировать на MonoBehaviour (lifetime GO ≠ lifetime World).

Редкое исключение наружу: `BuildingRejectedEvent` (тост). Не спавн, не тик дня.

Пауза/скорость — команды на `SimControl`, не запись стоков. Готовность забега — unmanaged lifecycle `SimSession.Phase`: `Unprepared`, `Preparing`, `Ready`, `Resetting`. Это не состояние часов.

Gameplay Presentation **никогда не запускает consume вручную**. Постановка дома, назначение рабочих и слайдеры пирамиды только вызывают `SimCommands.TryPost`; команды применяет следующий тик `CommandSystemGroup`.

Исключений для ручного playback нет. `RunPublisher.BeginRun`, `RunSessionSnapshot.BeginApply` и `RunPublisher.BeginReset` только атомарно ставят входящие команды и фазу. Линейный `CommandSystemGroup` исполняет их, lifecycle-finalizer подтверждает `Ready` / `Unprepared`, а `GameSession` асинхронно ждёт подтверждение с timeout/cancellation. Loading и gameplay input снимаются только после `Ready`; Game scene выгружается только после `Unprepared`.

## Меши и поза

> Поза в ECS — `LocalTransform`. Дом: sim-entity (пакеты, скопированные со spec) + живой GO-вид (меш, Animator, Light). Light/Animator не печь в ECS.  
> Человек: `Instantiate(SimPrototypes.Agent)` + `AgentType`; Mixamo GO копирует позу, пока жив Animator.

Не кладём в entity: `Transform`, `Animator`, `GameObject`, слот Mixamo.  
Тип агента (`AgentType`) — да. Какой FBX — таблица Presentation (`AgentKind` → префабы, скин стабилен от `AgentId`).

## Потоки

```text
кнопка spawn / place
  → HUD каталог: ключ из BuildingPrototype, имя с BuildingView
  → SimCommands.TryPost (буфер на session)
  → CommandSystemGroup
  → агент: Instantiate(SimPrototypes.Agent)
  → дом: CreateEntity из BuildingPrototype + Building; Construction пока не построен
       complete → снять Construction (тот же entity)

вид (LateUpdate pull)
  ← query какие entity есть
  ← доска Instantiate(Unity-штамп) один раз; `BuildingView.Sync` читает свою entity
  ← стройка / загрузка: бар на WorldUi ребёнке штампа
  ← зона `_BuildingOverlay` (клетка, не дом)
  ← HQ: `_HqOverlay` (кольцо + клик)
  ← люди: AgentViewBoard — LocalTransform + Moving → Mixamo; Arrived → меш выключен
  ← сток HUD pull ResourceAmount
  ← слот дома: BuildingInspectPanel +/− → Assign/Unassign
```

Отказ стройки — `BuildingRejectedEvent` → `BuildingRejectLog`. Спавн/день — не события.

## Что где

| Кусок | Слой |
| --- | --- |
| `SpawnAgentCommand`, `PlaceBuildingCommand`, `AssignWorkerCommand`, `SimCommands` | Simulation |
| `LocalTransform`, `AgentLocomotion`, `AgentAssignment`, `Workplace`, `ResourceAmount` | Simulation |
| Session singleton | SubScene: `SimControlAuthoring` печёт `SimSession`, `SimControl`, sequence и lifecycle buffers |
| Плаза HQ | SubScene bake; не `PlaceBuilding` |
| `AgentViewBoard` / `BuildingViewBoard` / `BuildingSelection` / `TimeWidget` | Presentation **читает** World |
| Пауза / x1 x2 x3 | `SimClockCommand` → `SimControl` |

`SimSessionAccess` находит session singleton по `SimSession`. Генератор ID живёт отдельно в `AgentIdSequence`; reset — типизированные `DespawnAllAgentsCommand` → `DespawnAllBuildingsCommand`. Полный pipeline: clock → reset agents → reset buildings → scenario spawn → spawn → place → assign → unassign → workplace pause → pyramid feed → lifecycle finalizer → delta time. `BuildingRejectedEvent` — outbound и в drain-check не входит.

`AgentSpawner` только постит spawn (площадка/скорость). Префабы Mixamo и `spawnParent` — на `AgentViewBoard` (тот же GO).

Связанные: [[08 Production ECS]] · [[10 Vertical Slice — Shell + ECS Walkers]] · [[13 Time HUD and Save]]
