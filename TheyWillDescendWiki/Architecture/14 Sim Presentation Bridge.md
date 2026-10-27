# 14 Sim ↔ Presentation Bridge

← [[13 Time HUD and Save]] | [[Index]] | [[../Home|Home]]

## Закон (однонаправленно)

World — единственный write model игры. Стрелки не симметричны:

```text
Presentation  →  поле желания на уже известной Entity   штат, пауза дома, слайдер
Presentation  →  запрос / SimCommands                   поставить, снести, спавн, оплата, часы, ритуал
Presentation  ←  query / pull                           единственное чтение в вид
Simulation    ✕  TimeWidget / ViewBoard / Animator
```

`SystemBase` в Presentation, который вызывает `TimeWidget.ShowTime` или `*ViewBoard.Sync`, — это **обратная** связь: ECS-тик владеет видом. Так нельзя. Канон: виджет/доска в `Update` / `LateUpdate` сами читают World. Query не кэшировать на MonoBehaviour (lifetime GO ≠ lifetime World). Если список всё же нужен (живые диалоги, квесты, тех-карты) — кэш на `SimWorld`, у него то же время жизни, что у World.

HUD (`Presentation/GameHud`) не делает `CreateEntityQuery`. Карточка дома читает `BuildingSelection.Selected` и компоненты этой сущности; idle — `WorkforceSummary` на сессии; бригада стройки — `Construction.CrewAssigned` / `CrewArrived`; пирамида — `CityCore.Headquarters`; часы — `GameTime` на сессии через `SimWorld`. Доски видов (`BuildingViewBoard`, `AgentViewBoard`) мир сканировать могут: это синхронизация GO, не HUD.

Редкое исключение наружу: `BuildingRejectedEvent` (тост). Не спавн, не тик дня.

Пауза/скорость — буфер `SimClockCommand` на сессии, не запись стоков. Ран готов, когда `GameSession.Begin` синхронно собрал мир (`RunWorld.Wipe` + bootstrap + scenario/save). Фаз `SimSession.Phase` нет.

Gameplay Presentation **никогда не запускает** `Process*` вручную.

Два входа, оба канон:

- **Желание.** Игрок уже держит `Entity` (выбор луча, штаб). Кнопка пишет только поле входа: `Workplace.DesiredWorkers`, `Workplace.Paused`. Система желание не трогает. `+` серый только при `DesiredWorkers >= slots` — свободные люди не блокируют заказ. Недобор виден в строке `{assigned} / {desired}` и в `WorkforceSummary`. Следующий тик диспетчер добирает и отпускает людей.
- **Разовое действие.** Поставить, снести, заспавнить, дорога, исследование. Request-сущность через `SimCommands.Request`. `CommandSystemGroup` обрабатывает на следующем тике (`Process*RequestsSystem`).
- **Поток за кадр.** Часы и story UI — буфер на сессии (`SimCommands.TryPost`). Порядок постановки сохраняется. Часы — не request.

| Канал | Примеры |
| --- | --- |
| Поле на entity | `Workplace.DesiredWorkers`, `Workplace.Paused` |
| Request | `PlaceBuildingRequest`, `DemolishBuildingRequest`, `SpawnAgentRequest`, `PlaceRoadStrokeRequest`, `DemolishRoadRequest`, `SetActiveResearchRequest`. `SetPyramidFeedRequest` — временный долг (слайдеры не трогать) |
| Буфер на сессии | `SimClockCommand`, `StoryUiCommand` |
| Событие наружу | `BuildingRejectedEvent` |

Выбор дома — `BuildingSelection.Selected` / `Hovered` как `Entity`. Клик читает `BuildingIdTag.Target`. Инспектор не ищет дом по `Building.Id`: `em.Exists(Selected)` и компоненты этой сущности. Снос: `DemolishBuildingRequest.Building`. JSON сейва `Entity` не хранит.

Итоги UI не пишет: склад, лояльность, позу, `WorkingCount`. Кнопка может прочитать мир, чтобы посереть. Ограничение (нельзя желать больше свободных людей, чем есть) остаётся в системе. Часы — запрос, потому что это режим тика, а не поле одного дома.

Исключений для ручного playback нет. `GameSession.Begin` синхронно: wipe → bootstrap → `ScenarioSetup` или `SnapshotRestore.Apply`. Teardown — `RunWorld.Wipe` в `Shutdown` до выгрузки сцены.

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
  ← стройка / загрузка: бар на Widget-ребёнке штампа
  ← зона `_BuildingOverlay` (клетка, не дом)
  ← HQ: `_HqOverlay` (кольцо + клик)
  ← люди: AgentViewBoard — LocalTransform + Moving → Mixamo; Arrived (дом или стройка) → меш выключен
  ← сток HUD pull ResourceAmount
  ← слот дома: BuildingInspectPanel +/− → Workplace.DesiredWorkers
```

Отказ стройки — `BuildingRejectedEvent` → `BuildingRejectLog`. Спавн/день — не события.

## Что где

| Кусок | Слой |
| --- | --- |
| `SpawnAgentRequest`, `PlaceBuildingRequest`, `DemolishBuildingRequest`, `SimCommands` | Simulation |
| `LocalTransform`, `AgentLocomotion`, `AgentAssignment`, `Workplace`, `ResourceAmount` | Simulation |
| Session singleton | runtime `SimulationBootstrap`: `SimSession`, `SimControl`, `CityCore`, каталоги |
| Плаза HQ | `CreateHeadquarters` в bootstrap; не `PlaceBuildingRequest` |
| `AgentViewBoard` / `BuildingViewBoard` / `BuildingSelection` / `TimeWidget` | Presentation **читает** World |
| Пауза / x1 x2 x3 | `SimClockCommand` → `SimControl` |

`SimSessionAccess` находит session по `SimSession`. Генератор ID — `AgentIdSequence` / `BuildingIdSequence`. Wipe рана — тег `RunScoped`, не `UniversalQuery`. Тик: `CommandSystemGroup` (clock buffer + `Process*RequestsSystem`) → commute/construction → production → day clock. `BuildingRejectedEvent` — outbound.

`AgentSpawner` только постит spawn (площадка/скорость). Префабы Mixamo и `spawnParent` — на `AgentViewBoard` (тот же GO).

Связанные: [[08 Production ECS]] · [[13 Time HUD and Save]]
