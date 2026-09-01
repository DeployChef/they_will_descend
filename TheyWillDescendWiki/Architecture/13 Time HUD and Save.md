# 13 Time HUD & Save (срез)

← [[12 Radial City Grid]] | [[Index]] | [[../Home|Home]]

Тулза времени как во Frostpunk и однослотовый save/load. Сверено с кодом.

## Закон

> Шелл желает (кнопки → команды). Симуляция хранит истину. UI только показывает и шлёт intent.  
> Save пишет **write model**, не GameObject’ы с экрана. Load **перестраивает** вид.

`Time.timeScale` не используем.

Истина часов — `SimControl` в ECS. Нет отдельного C#-`SimGate` и нет второго компонента `SimClock`.

Speed **не затирают** стройкой. Тик = функция `(SessionInGame, Speed, PlayerPaused, BuildLocked)`.

Три вещи, которые нельзя смешивать:

| Забота | Владелец | Не владелец |
| --- | --- | --- |
| Поток продукта (меню / сессия) | `AppStateMachine` | тулза времени |
| Тулза времени (⏸ x1 x2 x3) | HUD → `SimClockCommand` | отдельный `IAppState` |
| Модалка стройки | HUD → `SetBuildLocked` | новый `IAppState` |

Во Frostpunk пауза времени ≠ меню паузы. Esc и ⏸ — **Player-lock на вентиле**, стейт остаётся `Playing`. Меню Save/Quit — позже оверлей, не часы. `PausedState` в коде нет.

---

## 1. Тулза времени (HUD сверху)

Как во Frostpunk: пауза + три скорости + часы дня.

### Две паузы, не одна

Игрок на x3 открыл стройку → мир встаёт, **кнопки x1/x2/x3 серые**. Поставил дом (или Esc) → снова x3, не x1.

Это не «Set(Frozen) / Set(Running)». Иначе стройка затирает скорость.

| Вид | Кто ставит | Скорости на тулзе | Что помним |
| --- | --- | --- | --- |
| **Пауза игрока** (кнопка ⏸ / Esc) | игрок | **можно** нажать x1/x2/x3 — это снять паузу и ехать с этой скоростью | Speed пишется только здесь |
| **Модальная пауза** (каталог / ghost дома) | UI стройки | **нельзя** переключить скорость | Speed не трогаем; по выходу снимаем только этот замок |

`SimControl`:

```text
Speed          // последняя выбранная 1|2|3, стройка не затирает
PlayerPaused   // кнопка ⏸ / Esc
BuildLocked    // каталог / ghost
SessionInGame  // Playing vs меню
Mode           // Off | Frozen если любой замок | Running
DeltaTime      // кадр × Speed; системы сами skip, если не Running
```

Два bool, не `[Flags]`: обычный enum не может быть сразу Player и Build.

- Открыл каталог → `BuildLocked = 1`
- Esc / закрыл стройку → `BuildLocked = 0`; если PlayerPaused всё ещё 1 — мир стоит
- x1/x2/x3 при BuildLocked — ignore; иначе Speed=n и PlayerPaused=0
- Pause при BuildLocked — ignore

Esc в `Playing`: сначала `BuildWidget.TryHandleEscape()`, иначе `TogglePause()`. **Не** `TransitionTo(Paused)`.

API вентиля — `SimClockCommand`:

```text
SetSessionInGame
SetSpeed(1|2|3)
TogglePlayerPause
SetBuildLocked
Restore (load)
```

HUD **никогда** не пишет `Mode` вручную. `ConsumeSimClockCommandsSystem` считает Mode из флагов.

### Данные в ECS

Один синглтон часов:

```text
SimControl
  Mode, Speed, DeltaTime
  SessionInGame, PlayerPaused, BuildLocked
```

`ApplySimDeltaTime` (последний в `CommandSystemGroup`) пишет `DeltaTime = frame * Speed`.  
**Не** ноль на паузе. `AdvanceGameTime` и ходьба пьют `DeltaTime` **и** проверяют `IsRunning`.

Презентация ходоков: `Animator.speed` следует Running/Speed (вид pull).

Грамматика «entity хранит, system меняет» — `GameTime` + `AdvanceGameTimeSystem`, и тот же session для `SimControl`.

### Часы на HUD (как «день идёт»)

`GameTime`: `Day`, `ElapsedInDay`, `DayDuration`.

Проекция для UI (считать в Presentation, не в системе экономики):

```text
hours01 = ElapsedInDay / DayDuration          // 0..1 внутри дня
clockHours = hours01 * 24
HH:MM из clockHours
подпись: Day {Day}   {HH}:{MM}
```

Сейчас `DayDuration` берётся из `DefaultSimRules` (60 с на x1). x2/x3 ускоряют тот же день, не длину суток в ассете.

HUD **читает** `GameTime` + `SimControl`. Кнопки → `SimClockCommand`.

Подсветка: активна последняя Speed даже на модальной паузе; сами кнопки disabled, пока висит `Build`.

### Что не класть в тулзу

- Камера, меню, FMOD pitch не ускоряются через `timeScale`
- `Set(Running)` по закрытию каталога — ломает x3 и игнорирует Esc-pause
- Музыка: `GameAudio` паузит инстанс по `PlayerPaused`, не по Speed

---

## 2. Что значит «сохранить всё, что на экране»

На экране **смесь**. Save обязан это различать.

| Что видишь | Где живёт | В слот? |
| --- | --- | --- |
| Деревья, земля Sample | сцена `Game.unity` | нет — уровень, не ран |
| `GameTime` (день, прогресс суток) | ECS singleton | **да** |
| Пауза / скорость | `SimControl` | **да** |
| Челики | ECS: `LocalTransform` + `AgentLocomotion` + `AgentType` | **да** |
| Поставленные дома | ECS: `Building` + опционально `Construction` | **да** |
| Occupy | `OccupiedCell` на session | восстанавливается из домов |
| Призрак размещения | UI-режим | **нет** — перед save отменить placing |
| Камера | Presentation | нет в v0 |
| Служебные Unity entities | Default World | **нет** |

Итог: save — не скриншот и не `DontDestroyOnLoad`. Это **снимок рана**.

---

## 3. Как save/load делается вообще (карта вариантов)

### A. Свой payload (берём сегодня)

Явный JSON известных типов. Load: очистить динамику → проставить синглтоны → заспавнить виды из записей.

Плюс: понятно, версионируется, не ломается о hybrid.  
Минус: каждый новый домен добавляет поля в payload — это нормально.

### B. Сериализация World (`SerializeUtility`)

Официальный DOTS-путь. Сейчас **не берём**: служебные сущности Unity + hybrid-виды.

Когда write model целиком в ECS — B станет каноном **поверх** тех же границ: query динамики, не SubScene-статика.

### C. Сохранить сцену / `Instantiate` иерархии

Антиканон. Пиклить GameObject = второй write model. Не делаем.

---

## 4. Контракт слота (один файл)

Один слот, кнопки «Сохранить» / «Загрузить» на Game HUD. Путь `persistentDataPath/run_slot0.json`.

```text
SavePayload v15
  version: 15
  clock, time
  resources: [{ resourceId: "wood", amount }]
  agents: [{ id, pose, motor, assignment, plaza idle }]
  buildings: [{ id, typeId: "sawmill", footprint, built, construction, workerAgentId }]
```

Пока разрабатываем — **миграций нет**. Слот с другим `version` удаляется при load.

`built = 0` → load `CreateEntity` из spec с `Construction` (тот же меш на доске, бар World UI).  
`built = 1` → сразу готовый дом (без `Construction`).

Канон агента: **`LocalTransform` = где стоит**. Мотор = `AgentLocomotion`. Без работы — `AgentPlazaIdle`. На слоте — `AgentAssignment`. Сток — `ResourceAmount` на session.

`AgentId` — ключ моста на ран, не `Entity(53,1)`. После перезапуска Unity номера сущностей другие.

### Идентичность: не `53.1`

`53.1` = слот.версия в **этом** мире, в **этом** Play. После Stop слот переиспользуют.

| Задача | Инструмент | Живёт в билде? |
| --- | --- | --- |
| Понять в Hierarchy, кто это | `EntityManager.SetName` — editor-debug | обычно нет |
| Узнать тип в Inspector | компоненты | да |
| Пережить save/load | **свой** стабильный id (`AgentId` / `BuildingId` / `typeId`) | да |

### Save (последовательность)

1. UI → `SaveWidget` (не считает экономику).
2. Отменить ghost-placing, если открыт.
3. Прочитать синглтоны из ECS (`RunSessionSnapshot`).
4. Собрать агентов и дома query’ем.
5. Атомарно записать файл. Залог `GameLog`.

### Load (последовательность)

1. Нет файла → лог, ничего не трогать.
2. На время применения: команды + `SimCommands.Playback()` в том же кадре (не «полкадра дыры»).
3. Снести **только динамику рана**: `DespawnAllAgents` / `DespawnAllBuildings`. Не выгружать `Game.unity`.
4. Записать `GameTime` + Speed/PlayerPaused через `SimClockCommand.Restore`.
5. Заспавнить агентов и дома теми же командами, что кнопка/placement, с полями из файла.
6. Восстановить Mode как в файле («тот же кадр»).

Load **не** = `GameSession.Dispose` + полный reload сцены.

После load `SaveWidget` качает `AgentViewBoard` и `BuildingViewBoard.RebuildViews()`.

---

## 5. Слои (куда класть)

| Кусок | Слой |
| --- | --- |
| Тулза времени | Presentation (`TimeWidget` на `TimeBar`) |
| Слот save/load | Presentation (`SaveWidget`) |
| Спавн агента | Presentation (`AgentSpawner` → команда; вид — `AgentViewBoard`) |
| Каталог / ghost стройки | Presentation (`BuildWidget`; Esc и `BuildLocked`) |
| Часы | Simulation `SimControl` + `GameTime` |
| Файл, версия payload | Presentation / `Infrastructure` (`RunSnapshotStore`) |
| Сбор/применение snapshot | Presentation / `Application` (`RunSessionSnapshot`) |

Антипаттерн: `Button.onClick` → `File.WriteAllText` + `FindObjectsOfType` по всем Transform.  
Антипаттерн: `ISystem` пишет на диск.

---

## 6. Временное vs канон

| Сейчас (срез) | Позже |
| --- | --- |
| JSON один слот | слоты, имя, скриншот, checksum |
| Дома и occupy в ECS | тот же мост |
| Агенты: позиция + ключ вида в ECS, меш снаружи | тот же мост, когда появится pathfinding |
| Свой payload | опционально `SerializeUtility` по query динамики |
| `DayDuration` 60 с | более длинный боевой день |
| Цифровые HH:MM | при желании кольцо-часы как FP |

---

## 7. Чеклист Editor

1. Верх Game HUD: Pause, x1, x2, x3, текст `Day N  HH:MM`.
2. Save / Load — две кнопки, без красивого меню.
3. Play: день на x3 бежит втрое быстрее, чем на x1; пауза стопит часы и ходоков, скорость подсвечена прежняя.
4. Заспавнить 2 челиков, поставить 1 дом, крутануть день, Save, доспавнить ещё, Load — снова те же 2 челика, 1 дом, тот же день.
5. Выключить Play, включить снова, Load — слот с диска жив.

Критерий провала: после Load дома на месте, а occupy пустой (можно ставить второе здание в ту же клетку).

---

## Контрольные вопросы

1. Почему деревья Sample не пишутся в JSON?
2. Почему `Entity(53,1)` нельзя класть в файл как id челика?
3. Если save сделать через `timeScale` и `DontDestroy` иерархии — какой закон слоёв ломается?
4. На паузе игрока Speed=3. Что показывает тулза? Что лежит в `SimControl.DeltaTime`? Почему сутки всё равно стоят?
5. Игрок на x3 открыл стройку. Можно ли нажать x1? Что будет после постановки дома?

Связанные: [[03 Core Systems]] · [[09 App Shell]] · [[08 Production ECS]] · [[10 Vertical Slice — Shell + ECS Walkers]] · [[../GDD/08 UI & Visual|GDD UI]]
