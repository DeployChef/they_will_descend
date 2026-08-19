# 13 Time HUD & Save (срез)

← [[12 Radial City Grid]] | [[Index]] | [[../Home|Home]]

Урок + контракт на ближайший заход: **тулза времени как во Frostpunk** и **однослотовый save/load**.  
Код — после согласования этой страницы.

## Закон

> Шелл желает (кнопки). Симуляция хранит истину. UI только показывает и шлёт intent.  
> Save пишет **write model**, не GameObject’ы с экрана. Load **перестраивает** вид.

`Time.timeScale` не используем.

**Скорость и замки паузы живут в Shell (`SimGate`), не в ECS.** ECS — тупое зеркало EffectiveMode + Speed.  
Не «запомнить скорость и вызвать её обратно». Speed **не затирают**; тик = функция `(сессия, Speed, PlayerPaused, BuildLocked)`.

Три вещи, которые нельзя смешивать:

| Забота | Владелец | Не владелец |
| --- | --- | --- |
| Поток продукта (меню / сессия) | `AppStateMachine` | тулза времени |
| Тулза времени (⏸ x1 x2 x3) | `SimGate` | ECS, отдельный `IAppState` |
| Модалка стройки | HUD → `SetBuildLocked` | новый `IAppState` |

Во Frostpunk пауза времени ≠ меню паузы. Esc и ⏸ — **Player-lock на вентиле**, стейт остаётся `Playing`. Меню Save/Quit — позже оверлей, не часы. `PausedState` в коде нет.


---

## 1. Тулза времени (HUD сверху)

Как во Frostpunk: пауза + три скорости + часы дня.

### Две паузы, не одна

Игрок на x3 открыл стройку → мир встаёт, **кнопки x1/x2/x3 серые**. Поставил дом (или Esc) → снова x3, не x1.

Это не `Set(Frozen)` / `Set(Running)`. Иначе стройка затирает скорость (сейчас HUD так и делает: выход из каталога всегда `Running`).

| Вид | Кто ставит | Скорости на тулзе | Что помним |
| --- | --- | --- | --- |
| **Пауза игрока** (кнопка ⏸ / Esc) | игрок | **можно** нажать x1/x2/x3 — это снять паузу и ехать с этой скоростью | Speed пишется только здесь |
| **Модальная пауза** (каталог / ghost дома) | UI стройки | **нельзя** переключить скорость | Speed не трогаем; по выходу снимаем только этот замок |

`SimGate`:

```text
Speed          // последняя выбранная 1|2|3, стройка не затирает
PlayerPaused   // кнопка ⏸ / Esc
BuildLocked    // каталог / ghost
EffectiveMode  // Off | Frozen если любой bool | Running
```

Два bool, не `[Flags]`: обычный enum не может быть сразу Player и Build. Флаги для двух бит — лишняя форма. Два независимых вопроса «игрок на паузе?» и «стройка открыта?».

- Открыл каталог → `BuildLocked = true`
- Esc / закрыл стройку → `BuildLocked = false`; если PlayerPaused всё ещё true — мир стоит
- x1/x2/x3 при BuildLocked — ignore; иначе Speed=n и PlayerPaused=false
- Pause при BuildLocked — ignore

Esc в `Playing` → `TogglePlayerPause()`, **не** `TransitionTo(Paused)`.

API вентиля:

```text
SetSessionInGame(bool)
SetSpeed(1|2|3)
TogglePlayerPause()
SetBuildLocked(bool)
EffectiveMode
```

HUD **никогда** не вызывает `Set(Running)` / `Set(Frozen)`.

Не восстанавливаем скорость вызовом. Сняли `BuildLocked` → Effective сам Running×3, если игрок не на паузе.


### Данные в ECS

Синглтон часов (не плодить второй Mode рядом с `SimControl` — при коде слить или sync писать EffectiveMode):

```text
SimClock
  Mode           // EffectiveMode
  Speed          // 1, 2, 3 (последняя игрока)
  DeltaTime      // Running ? unscaledDt * Speed : 0
```

Одна система (Initialization, после sync) считает `DeltaTime`.  
`AdvanceGameTime` и ходьба пьют **его**, не `SystemAPI.Time.DeltaTime`.

Презентация ходоков: `Animator.speed = Mode==Running ? Speed : 0`.

Грамматика «entity хранит, system меняет» — это уже `GameTime` + `AdvanceGameTimeSystem`. Туда же зеркало `SimClock`.  
**Не** класть в ECS замки стройки и «кто нажал x3»: это пульт, не сутки города. Иначе каждая модалка (дом, закон, кризис) рождает UI-компоненты в симуляции, а системы начнут хранить состояние — а хранят только компоненты.

Пульт = `SimGate`. Мост копирует Effective. `AdvanceGameTime` только прибавляет `clock.DeltaTime`.

### Часы на HUD (как «день идёт»)

`GameTime` уже есть: `Day`, `ElapsedInDay`, `DayDuration`.

Проекция для UI (считать в Presentation, не в системе экономики):

```text
hours01 = ElapsedInDay / DayDuration          // 0..1 внутри дня
clockHours = hours01 * 24
HH:MM из clockHours
подпись: Day {Day}   {HH}:{MM}
```

Сейчас `DayDuration ≈ 5` сек — стрелка/цифры будут быстро бежать. Это **хорошо** для проверки. Позже баланс растянет сутки. Не хардкодить «1 реальная секунда = 1 час» в кнопке.

HUD **читает** `GameTime` + `SimClock` (или через тонкий binder). Не пишет в них с Update, кроме кнопок → SimGate.

Подсветка: активна последняя Speed даже на модальной паузе; сами кнопки disabled, пока висит `Build`.

### Что не класть в тулзу

- Камера, меню, FMOD pitch не ускоряются через `timeScale`
- `gate.Set(Running)` по закрытию каталога — ломает x3 и игнорирует Esc-pause

---

## 2. Что значит «сохранить всё, что на экране»

На экране сейчас **смесь**. Save обязан это различать.

| Что видишь | Где живёт сейчас | В слот? |
| --- | --- | --- |
| Деревья, земля Sample | сцена `Game.unity` | нет — это уровень, не ран |
| `GameTime` (день, прогресс суток) | ECS singleton | **да** |
| Пауза / скорость | `SimGate` → `SimControl` / `SimClock` | **да** |
| Челики | ECS: `LocalTransform` + `CircleWalk` + `AgentType` | **да** |
| Поставленные дома | только Presentation (`placedRoot` + `_occupied`) | **да, как записи построек** (временно не ECS) |
| Призрак размещения | UI-режим | **нет** — перед save отменить placing |
| Камера | Presentation | нет в v0 (окно, не мир) |
| Служебные Unity entities (`51.1` без ваших компонентов) | Default World | **нет** |

Итог: save — не скриншот и не `DontDestroyOnLoad`. Это **снимок рана**: часы + динамика, которую мы сами породили после load Game.

---

## 3. Как save/load делается вообще (карта вариантов)

### A. Свой payload (берём сегодня)

Явный JSON/бинарник известных типов. Load: очистить динамику → проставить синглтоны → заспавнить виды из записей.

Плюс: понятно, версионируется, не ломается о hybrid.  
Минус: каждый новый домен (склад, рабочие) добавляет поля в payload — это нормально и честно.

### B. Сериализация World (`SerializeUtility`)

Официальный DOTS-путь: дамп entities в бинарник.

Сейчас **не берём**: дома ещё не entities, плюс куча служебных сущностей Unity. Получится хрупкий дамп «всего Default World».

Когда write model целиком в ECS (здания, слоты, стоки) — B станет каноном **поверх** тех же границ: сериализуем query динамики, не SubScene-статику.

### C. Сохранить сцену / `Instantiate` иерархии

Антиканон. Пиклить GameObject = второй write model. Не делаем.

---

## 4. Контракт слота v0 (один файл)

Один слот, кнопки «Сохранить» / «Загрузить» на Game HUD. Путь вроде `persistentDataPath/run_slot0.json`.

```text
SavePayload v3
  version: 3
  clock:  { speed, playerPaused }
  time:   { day, elapsedInDay, dayDuration }
  agents: [{ pose (pos+fwd), circle (temporary behavior) }]
  buildings: [{ width, depth, anchorCluster, anchorRadial }]
```

Канон агента: **`LocalTransform` = где стоит entity**. `CircleWalk` = временный рецепт круга. `AgentType` = кто это (Worker…), не слот Mixamo.

`AgentId` — ключ моста на ран, не `Entity(53,1)`. После перезапуска Unity номера сущностей другие.

### Идентичность: не `53.1`

`53.1` = слот.версия в **этом** мире, в **этом** Play. После Stop слот переиспользуют.

Два разных «по-другому», не путать:

| Задача | Инструмент | Живёт в билде? |
| --- | --- | --- |
| Понять в Hierarchy, кто это | `EntityManager.SetName(entity, "Walker_Maya")` — editor-debug | обычно нет |
| Узнать тип в Inspector | компоненты (`CircleWalk`, `GameTime`) | да |
| Пережить save/load | **свой** стабильный id: имя префаба / `BuildingId` / guid рана | да, это поле payload |

Запечённые `GameTime` / `SimControl` уже имеют имя с GameObject SubScene. Безымянные ходоки — потому что `CreateEntity()` без `SetName`. Для слота имени мало: нужен id контента на компоненте, который мы сами пишем при спавне.

### Save (последовательность)

1. UI → intent `SaveSlot0` (не считает экономику).
2. Отменить ghost-placing, если открыт.
3. Прочитать синглтоны из ECS.
4. Собрать агентов из query `LocalTransform` + `CircleWalk` + `AgentType`.
5. Собрать дома из **явного списка записей**.  
   **Временно:** список живёт у placement (или тонкий `BuildingSandboxRegistry`).  
   **Канон позже:** occupancy/building entity в ECS, save читает только ECS.
6. Атомарно записать файл. Залог `GameLog`.

### Load (последовательность)

1. Intent `LoadSlot0`. Нет файла → лог, ничего не трогать.
2. `Frozen` на время применения (кадр без тика посередине).
3. Снести **только динамику рана**: spawned agents (GO+entity), placed buildings, occupancy.  
   Не выгружать `Game.unity`, не трогать SubScene authoring.
4. Записать `GameTime` + Mode/Speed в ECS и в `SimGate` (шелл и симуляция не разъедутся).
5. Заспавнить агентов тем же пайплайном, что кнопка спавна, но с полями из файла (не Random), включая `agentType`.
6. Поставить дома тем же пайплайном, что `PlaceBuilding`, из записей.
7. Восстановить Mode из файла (если сохраняли Running x2 — после load снова Running x2). Либо оставить Frozen, чтобы осмотреться — выбрать одно в коде и не смешивать. **Предпочтение среза:** восстановить Mode как в файле («тот же кадр»).

Load **не** = `GameSession.Dispose` + полный reload сцены, пока слот простой. Reload сцены — ядерный вариант «с нуля», здесь достаточно reset динамики.

---

## 5. Дыры, которые надо закрыть в коде (не в UI)

Чтобы агенты переживали слот:

- `LocalTransform` — где стоит  
- `CircleWalk` — временное поведение круга  
- `AgentType` — кто это (`Worker`…). Скин Mixamo выбирает Presentation.

Дома: placement должен уметь **ClearAll** и **PlaceFromRecord**, не только клик мыши. Occupancy восстанавливается из тех же ячеек.

Это не «сохранение сцены». Это границы, без которых load не соберёт картинку.

---

## 6. Слои (куда класть)

| Кусок | Слой |
| --- | --- |
| Кнопки паузы/скорости/save/load, текст часов | Presentation (`GameHudBinder` + разметка Canvas) |
| `SimGate.Set(Mode/Speed)` | Presentation / `Shell` (`SimGate`) |
| `SimClock`, `GameTime` | Simulation |
| Читать/писать файл, версия payload | Presentation / `Infrastructure` (`RunSnapshotStore`) |
| Сбор/применение snapshot | Presentation / `Application` (`RunSessionSnapshot`): не экономика, только map ECS ↔ payload |

Антипаттерн: `Button.onClick` → `File.WriteAllText` + `FindObjectsOfType` по всем Transform.  
Антипаттерн: `ISystem` пишет на диск.

---

## 7. Временное vs канон

| Сейчас (срез) | Позже |
| --- | --- |
| JSON один слот | слоты, имя, скриншот, checksum |
| Дома в registry Presentation | building entities + occupancy в ECS, save только оттуда |
| Агенты: позиция + ключ вида в ECS, меш снаружи | тот же мост, когда появится pathfinding |
| Свой payload | опционально `SerializeUtility` по query динамики |
| `DayDuration` короткий | баланс суток |
| Цифровые HH:MM | при желании кольцо-часы как FP |

Помечать в коде комментарием, если registry домов ещё не ECS.

---

## 8. Чеклист Editor (когда начнём)

1. Верх Game HUD: Pause, x1, x2, x3, текст `Day N  HH:MM`.
2. Save / Load — две кнопки, без красивого меню.
3. Play: день на x3 бежит втрое быстрее, чем на x1; пауза стопит часы и ходоков, скорость подсвечена прежняя.
4. Заспавнить 2 челиков, поставить 1 дом, крутануть день, Save, доспавнить ещё, Load — снова те же 2 челика (те же префабы), 1 дом, тот же день/угол.
5. Выключить Play, включить снова, Load — слот с диска жив.

Критерий провала: после Load дома на месте, а `_occupied` пустой (можно ставить второе здание в ту же клетку). Occupancy входит в снимок.

---

## Контрольные вопросы

1. Почему деревья Sample не пишутся в JSON?
2. Почему `Entity(53,1)` нельзя класть в файл как id челика?
3. Если save сделать через `timeScale` и `DontDestroy` иерархии — какой закон слоёв ломается?
4. На паузе игрока Speed=3. Что показывает тулза и что лежит в `SimClock.DeltaTime`?
5. Игрок на x3 открыл стройку. Можно ли нажать x1? Что будет после постановки дома?

Связанные: [[03 Core Systems]] · [[09 App Shell]] · [[08 Production ECS]] · [[10 Vertical Slice — Shell + ECS Walkers]] · [[../GDD/08 UI & Visual|GDD UI]]
