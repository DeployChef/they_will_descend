# FMOD — полный разбор: как что работает, зачем и почему

← [[Home|Главная _Audio]]

> Статус: сверено с кодом ветки `sound` и метаданными `Audio_FMOD/they will desend/Metadata/`.
> Если код и этот документ расходятся — **истина в коде**.

---

## 1. Зачем в проекте FMOD и почему именно он

Проблема, из которой выросла вся система: в открытом мире с большой плотностью
застройки нельзя вешать по `AudioSource` на каждое здание. Старый подход
(перебор ячеек сетки) упирался в **14 592 потенциальных инстанса** — это перегрев
микшера и память. Нужен был инструмент, который умеет:

1. **Многослойные ивенты** — один ивент `Ambience_Town` внутри содержит ~10
   дорожек (разговоры, работа, огонь, общий фон). Включать/выключать слои
   дороже, чем плодить события.
2. **RTPC-параметры** — звук меняется непрерывно (громкость слоя = функция от
   параметра), без кода на каждый чих. Код говорит «в зоне 3 дома», FMOD сам
   решает, насколько громко звучат разговоры.
3. **Банки** — звук собирается в файлы, грузится пачками, выгружается пачками.
   Плюс ветка звукорежиссёра работает в FMOD Studio параллельно с кодом:
   правки звука **не требуют пересборки игры** — только Ctrl+B и копирование
   банков.
4. **Live Update** — FMOD Studio подключается к играющей игре (порт 9264) и
   показывает в реальном времени уровни дорожек и значения RTPC. Звук
   настраивается на слух без перезапуска.

Что выбрали вместо «звук на каждую ячейку»: **50 аудио-зон, один FMOD-инстанс
на зону**, плюс куллинг по конусу обзора камеры. Подробности ниже.

---

## 2. Карта системы: кто кого вызывает

```
Bootstrap-сцена
├── GameAudio                    музыка (сейчас ВЫКЛ) + снэпшоты паузы ESC/DEAFULT
├── GlobalAmbienceManager        ветер (event:/AMBIENCE/Ambience_Wind_Generation), persistent
├── UiAudioManager               создаёт себя сам, UI-звуки (event:/UI/UI), persistent
└── AudioZoneManager             хост 50 аудио-зон
    ├── AudioVisibilityChecker   конус камеры: зона слышна/глуха
    └── AudioZone[] (50 шт.)     каждая держит ОДИН FMOD EventInstance

Game-сцена (additive)
├── BuildingViewBoard (ECS→презентация)
│   ├── вешает BuildingAudioSource на постройки, ведёт CountsForAmbience (COMPLETE?)
│   └── дёргает BuildingConstructionAudio.SyncState каждый кадр
├── BuildingAudioSource          «я дом №N в зоне X» → зона пересчитывает RTPC
├── BuildingConstructionAudio    стройка: PLACEMENT → BUILD → COMPLETE
└── ZoneAmbienceSfxScheduler     «живчик»: одноразовые звуки по активным зонам

Общее (static)
└── FmodBankLoader               загрузка/выгрузка банков, поиск банков ивента
```

Simulation (DOTS) аудио **не знает**. Всё живёт в Presentation и читает
симуляцию только через `SimWorld.TryGet` (CityGrid, SimControl) и через
`BuildingViewBoard`. Аудио никогда не пишет в ECS.

---

## 3. Пайплайн: от FMOD Studio до игры

```
Audio_FMOD/they will desend/they will desend.fspro   ← проект звукорежиссёра
  Ctrl+B (Build)
Audio_FMOD/they will desend/Build/Desktop/*.bank      ← результат сборки
  КОПИРОВАНИЕ ВРУЧНУЮ (авто-копирования НЕТ)
TheyWillDescend/Assets/StreamingAssets/Desktop/*.bank ← что видит Unity
  RuntimeManager при старте сам грузит все банки (ImportType = StreamingAssets)
  FmodBankLoader — явная дозагрузка со страховкой + hot reload
```

Банки в `StreamingAssets/Desktop` сейчас:

| Банк | Что внутри | Кто грузит |
| --- | --- | --- |
| `Master` + `Master.strings` | микшер, пути ивентов | RuntimeManager сам; **никогда не выгружать** |
| `Ambience_Town` | `Ambience_Town`, `Ambience_Town_SFX_Random` | FmodBankLoader (через EventReference или поле `Fmod Banks`) |
| `Ambience_Wind_Generator` | `Ambience_Wind_Generation` (банк с «r», ивент без) | GlobalAmbienceManager по хардкоду |
| `BUILDING` | `BUILD` | BuildingConstructionAudio лениво |
| `UI` | `UI` | UiAudioManager |
| `Main_theme` | `main_soundtrack`, `ADAPTIVE_GENERATIVE MUSIC` | GameAudio (музыка выключена) |

**Почему Master.strings нельзя выгружать:** в нём лежат строковые пути всех
ивентов. Выгрузишь — резолв `event:/...` по имени умирает, `CreateInstance`
молча возвращает невалидный инстанс.

**Почему копирование руками — источник «у меня работает, у него нет»:** банк
старее метаданных → параметра в банке ещё нет → FMOD молчит. Первая проверка
любого «звук не меняется»: сравнить время изменения `.bank` в
`Build/Desktop` и `StreamingAssets/Desktop` с временем правки в FMOD.

---

## 4. Три уровня носителей звука

Система сознательно разделена на три уровня слышимости — по «размеру» звука:

| Уровень | Класс | Сколько инстансов | Когда живёт |
| --- | --- | --- | --- |
| Глобальный | GlobalAmbienceManager | 1 на ивент (ветер) | всегда (deathDistance = 0) |
| Зональный | AudioZoneManager → AudioZone | максимум 50 | зона в конусе камеры, не пустая, ближе смерти по дистанции |
| Точечный | BuildingConstructionAudio, ZoneAmbienceSfxScheduler, UiAudioManager | десятки | пока идёт процесс (стройка, вспышка UI) |

Зачем так: ветер не должен глохнуть при повороте камеры, а стук молотка не
должен жить вечно — у каждого типа звука своё правило жизни.

---

## 5. Аудио-зоны: сердце системы

### 5.1 Геометрия — 50 зон поверх городской сетки, 1 в 1

`AudioZoneManager` каждый кадр (до готовности) читает из DOTS компонент
`CityGrid`: центр, `InnerRadius`, внешний радиус = `InnerRadius + RingCount *
RadialStep`. Аудио-сетка строится поверх той же области:

- **10 угловых секторов** × 36° (`AudioZoneSettings.AngularSectors`);
- **5 радиальных полос** равной толщины `(внешний − внутренний) / 5`;
- итого **50 зон**, позиция инстанса — центр конуса (середина сектора,
  середина полосы).

Почему не «зона = ячейка сетки»: в одной ячейке живёт одна постройка, а звук
города — коллективный. Зона укрупняет ячейки до слышимого масштаба: 50
инстансов вместо 14 592, а детализация внутри зоны передаётся **RTPC**, а не
количеством инстансов.

Почему геометрия из `CityGrid`, а не из полей инспектора: сетка города может
меняться (кольца добавляются) — аудио перестраивается автоматически
(`SetGridExtent` вернул «изменилось» → пересоздание зон + перелинковка
построек `RelinkExistingSources`). Значения в ассете `AudioZoneSettings` —
только fallback на первые секунды, пока DOTS-сетка не готова (через 3 секунды
ожидания зоны строятся с центром (0,0,0) — чтобы гизмо и дебаг работали сразу).

`FindZoneNear(worldPos)` — **точное полярное попадание**: угол → сектор,
радиус → полоса, индекс = `sector * RadialBands + band`. Не «поиск ближайшего
центра» — O(1) арифметика. Позиция внутри плазы (ближе InnerRadius) или за
внешним радиусом кладётся в крайнюю полосу.

### 5.2 Жизненный цикл зоны: мёртвое состояние

Инстанс зоны живёт по трём гейтам:

```
SetActive(visible):
  ЗАПУСК    = visible && !IsActive && !IsEmpty() && ActivityLevel > 0.01
  ОСТАНОВ   = (!visible || IsEmpty() || ActivityLevel <= 0.01) && IsActive
```

1. **Конус камеры** (AudioVisibilityChecker) — зона вне горизонтального поля
   зрения → инстанс `stop(ALLOWFADEOUT)` + `release()`. Звук **не существует**,
   а не «тихо играет» — ноль нагрузки на микшер.
2. **Пустота** — в зоне нет ни одной законченной постройки → зона молчит.
3. **Дистанционная смерть** (`UpdateDistanceDeath`, отдельный LOD-куллинг):
   дальше `ZoneDeathDistance` (100 м) инстанс полностью освобождается;
   возрождается ближе чем `DeathDistance − Hysteresis` (95 м). Гистерезис
   нужен, чтобы инстанс не дёргался на границе.

Тик видимости — **каждые 2 кадра** (`UpdateVisibilityBatch`), не каждый кадр:
поворот камеры на полкадра ухо не замечает, а батч 50 зон дешевле вдвое.

Почему дистанционная смерть вернулась (раньше её убирали): она больше **не
гейтит слышимость**. Конус решает «слышно/не слышно» без дистанции (дальние
зоны в кадре звучат наравне с ближними — иначе при отдалении камеры слышен
только центр). А смерть по дистанции — это чистый performance-куллинг на
заведомо нерелевантных зонах. `MaxDistance` при этом используется только для
нормализации Distance RTPC.

### 5.3 Конус слышимости: AudioVisibilityChecker

Зона слышна, если угол между направлениями «куда смотрит камера» и «где зона»
меньше половины **горизонтального** FOV. Три ловушки, из-за которых это
пришлось делать именно так:

1. **`Camera.fieldOfView` в Unity — вертикальный.** Горизонтальный шире:
   `2 * atan(tan(vFov/2) * aspect)`. Считали по вертикальному — конус был
   узким, зоны глохли у центра кадра.
2. **Forward камеры выравнивается по горизонтали** (`y = 0`, normalize), и
   вектор до зоны тоже flattенится. RTS-камера смотрит вниз; без этого
   наклон съедал конус.
3. **Полный зум** — исключение: `RTSCameraController.IsFullyZoomedOut`
   (самый дальний шаг зума) глушит ВСЕ зоны. Логика: на максимальной высоте
   игрок видит всю карту, локальные зоны сливаются в шум — остаётся только
   глобальный амбиент.

Камера и `RTSCameraController` ищутся **лениво** (`Camera.main`,
`FindFirstObjectByType`): Game-сцена грузится additive позже Bootstrap, а
cross-scene ссылки в .unity не сериализуются.

### 5.4 RTPC: что зона говорит FMOD

`RecalculateParameters()` (вызывается при добавлении/удалении источника) считает
по всем **законченным** постройкам зоны (`CountsForAmbience == true`):

| Параметр FMOD | Откуда | Нормализация |
| --- | --- | --- |
| `Cell_Activity` | Σ `activityWeight` всех построек | / 5, clamp 0–1 |
| `Has_Houses` | количество House | / 5, clamp 0–1 |
| `Has_Workshops` | количество Workshop | / 5, clamp 0–1 |
| `Has_Market` | количество Market | / 5, clamp 0–1 |
| `Distance` | камера → центр зоны, каждый тик | `dist / OuterRadius * 100` (0–100) |

Зачем порог 5: зона из 5+ домов — уже «полный» городский гул, дальше громкость
не растёт (иначе центр города ревел бы против окраин).

Строящееся/демонтируемое здание **не считается** (`CountsForAmbience = false`):
Ambience_Town играет только после COMPLETE — стройка звучит собственным ивентом
BUILD (см. 5.6), и двойной счёт не нужен.

Дефицит на стороне FMOD (по метаданным на сегодня): из четырёх параметров
плотности в банке **отсутствуют** — Unity честно шлёт их, но automation вешать
нечем, вызовы молча ничего не меняют. Работает только `Distance`. Это пункт
дорожной карты звукорежиссёра ([[13 Дорожная карта]]).

### 5.5 BuildingAudioSource: как зона узнаёт про постройки

Компонент на префабе постройки. Всё, что он делает — сообщает зоне о себе:

- `OnEnable`/`OnDisable`/`OnDestroy` → `LinkedZone.Add/RemoveAudioSource(this)`
  → зона пересчитывает RTPC;
- `BuildingType` (House/Workshop/Market/Infrastructure/Decoration) → какой
  `Has_*` считать;
- `activityWeight` (0–1) → вклад в `Cell_Activity`;
- `CountsForAmbience` — ставит `BuildingViewBoard` из ECS каждый кадр
  (Construction снята = закончено).

`LinkedZone` зона проставляет сама через `FindZoneNear` — на сцене ничего не
линкуется руками. При пересборке сетки зон `RelinkExistingSources`
перелинковывает всё заново (старые объекты зон мертвы).

### 5.6 BuildingConstructionAudio: звук стройки

Ивент `event:/BUILDINGS/BUILD`, банк `BUILDING`, параметр `BUILDING` —
**labeled**: NONE=0 / PLACEMENT=1 / BUILD=2 / COMPLETE=3. Один ивент на все
фазы стройки — фаза выбирается параметром, сэмплы лежат на parameter sheet.

Состояние читается из ECS (`SyncState` вызывается `BuildingViewBoard` каждый
кадр):

- `Construction` нет → COMPLETE (ваншот, при загрузке сейва первый синк
  готового здания **не играется** — иначе каждый чих снапшота озвучивался бы);
- `IsDismantling` → NONE (тишина);
- `Elapsed == 0` → PLACEMENT (постановка, ваншот);
- `Elapsed > 0` → BUILD (луп, держится до конца стройки).

Гейтинг — через зону постройки: зона невидима → инстанс останавливается
(ваншоты PLACEMENT/COMPLETE в невидимой зоне **не доигрывают** — звук за
кадром городу не нужен), BUILD при возврате зоны в конус возобновляется.

Технические решения, которые стоит знать:

- Банк грузится **лениво** при первом инстансе (`EnsureInstance`), не на старте
  сцены — стройки может не быть вовсе.
- Параметр резолвится **по ID с Trim**: в FMOD-проекте у имени хвостовой пробел
  (`"BUILDING "`), строгий матч по имени не попадает.
- `ignoreseekspeed: true` — labeled-параметр должен ставиться мгновенно, Seek
  Speed растянул бы переход.
- Флаг READONLY проверяется при резолве: автоматический параметр из кода не
  управляется никогда — в лог уходит подсказка «сделай Game Parameter (User:
  Labeled)».

### 5.7 ZoneAmbienceSfxScheduler: «живчик города»

Амбиент из слоёв — статичная подложка. Чтобы город дышал, планировщик стреляет
**одноразовыми** звуками (`event:/AMBIENCE/Ambience_Town_SFX_Random`, длинные
цепочки ~72 c):

- Кандидаты — только **активные** зоны (`IsActive`): зона вне конуса или пустая
  уже молчит, отдельная проверка не нужна. Полный зум убивает всех кандидатов
  сам.
- Каждые `SfxRandomTickInterval` (2.5 с) по каждой активной зоне вне кулдауна —
  **независимый бросок** `Random.value ≤ chance` (0.25). Не квота: сколько
  зон в кадре, столько и бросков — плотность следует за плотностью застройки.
- После выстрела зона уходит в кулдаун `15 c ± jitter 50%` — зоны не стреляют
  залпом; при пересборке сетки стартовые кулдауны разбросаны случайно.
- Потолок `MaxConcurrent = 4` живых звуков на весь мир.
- Точка звука — `GetRandomSoundPosition`: у случайной живой постройки зоны с
  разбросом 1.5 м; если построек нет — случайная точка конуса зоны.
- Инстанс **не глушится** — доигрывает сам; освобождение когда FMOD сообщил
  `PLAYBACK_STATE.STOPPED`. `ForceReleaseTime` (длина из FMOD + запас ≥30 c) —
  только страховка от утечки, не длительность.
- `SilenceAll()` (IMMEDIATE + release) — для hot reload банков и уничтожения.

---

## 6. GlobalAmbienceManager: ветер

Глобальный амбиент вне города/зон. Один инстанс
`event:/AMBIENCE/Ambience_Wind_Generation`, банк `Ambience_Wind_Generator`
(банк с «r», ивент без — FMOD-конвенция не универсальная, имя банка захардкожено
отдельным массивом). Хардкод путей — сознательное решение: это вечные
системные звуки, им не нужны ни EventReference, ни инспектор.

Ключевое: ветер — **функция высоты камеры**, не расстояния до центра.

```
target = clamp01((высота_камеры − 8) / (65 − 8))    // 8 м = максимум приближения, 65 м = максимум отдаления
```

Почему высота, а не дистанция до центра города: панорама камеры по карте
(WASD) не должна менять ветер — погода одна на всю карту; меняется только
«насколько высоко мы парим». Диапазон 8–65 — inspector-поля `min/maxCameraDistance`.

Сглаживание — экспоненциальное, **асимметричное** (независимо от FPS):

- подъём (`smoothSpeedRise = 1.5`) — ветер разгоняется плавно, резкий зум вверх
  не даёт «удар»;
- спад (`smoothSpeed = 4`) — приземлился, ветер утих быстро.

Дальше — паттерн, который стоит перенять для всех глобальных параметров:

1. **Резолв ID при старте**: `getParameterDescriptionList` → сравнение имён
   через `Trim()` + `OrdinalIgnoreCase` → сохранить `PARAMETER_ID`. В проекте
   было два параметра «Distance» / «Distance␣» (хвостовой пробел) — строгий
   матч по имени молча не находил нужный.
2. **Установка по ID**: `StudioSystem.setParameterByID(id, v, ignoreseekspeed:
   true)` — обходим Seek Speed параметра, иначе значение «ползёт».
3. **Readback-диагностика** (раз в 60 кадров при `logActivity`): прочитали
   значение обратно, `sent X / readback Y`. Совпало — дошло (если звук не
   меняется, кривая виновата в FMOD); не совпало — автодамп всех глобальных
   параметров.

Прочее:

- `deathDistance = 0` — «вечный» режим (звучит всегда). Значение > 0 включило
  бы смерть/возрождение по дистанции с гистерезисом 5 м (механика уже в коде).
- Ивент 2D — `AttachInstanceToGameObject` не нужен, звучит у слушателя.
- Банк грузится явно в `Start()`, инстанс создаётся **корутиной через кадр** —
  `RuntimeManager` мог не успеть с банками.
- Инстанс маршрутизируется в шину `bus:/Ambience`: в обёртке 2.03 нет
  `setBusChannelGroup`, поэтому core-API: `Bus.unlockChannelGroup()` →
  `getChannelGroup()` у шины и у инстанса → `busGroup.addGroup(instGroup)` →
  `lockChannelGroup()`. Оплата `SetMuted(true/false)` одним вызовом — mute
  шины глушит весь амбиент, инстансы продолжают идти по таймлайну.
- Debug `Mouse Wheel Controls Distance` — ручной прогон параметра колесом
  (шаг 0.1), по умолчанию ВЫКЛ; в редакторе скролл читается только при фокусе
  на Game View (Background Behavior у Input System).
- `DontDestroyOnLoad` — переживает смену сцен.

---

## 7. GameAudio: музыка и пауза

Живёт на Root в Bootstrap, рядом с камерой и `StudioListener`. Симуляция его
не вызывает.

**Музыка сейчас выключена.** Флаг `enableMusic` (по умолчанию off): при off
`StartSessionMusic()` не запускает трек, `Awake` принудительно стопит, если
кто-то запустил раньше. Вернуть музыку — включить флаг в инспекторе.
`main_soundtrack` (банк `Main_theme`) — старая статичная тема.

**Снэпшоты паузы работают всегда**, независимо от музыки: `SimControl.
PlayerPaused` (ECS) → снэпшот `snapshot:/ESC` на паузе, `snapshot:/DEAFULT`
на выходе (имя с опечаткой — так объект называется в FMOD-проекте, путь
захардкожен как есть). В обёртке 2.03 нет `getSnapshot` — снэпшот резолвится
как обычный ивент через `StudioSystem.getEvent("snapshot:/...")` +
`createInstance` + `start`. Пауза самой музыки — `setPaused`, не pitch и не
`timeScale`: музыка не должна «замедляться» — она должна замирать.

---

## 8. UiAudioManager: один вечный инстанс на весь UI

Самый экономный паттерн системы: **ни одного инстанса на звук**. Один инстанс
`event:/UI/UI` (ивент вечный, Persistent), звук выбирается labeled-параметром
`UI_States` (NONE=0 / HOVER=1 / YES=2 / NO=3 / WARNING=4 / CLICK=5) —
инструменты лежат на parameter sheet и триггерятся при входе значения в их
колонку. `start()` вызывается ровно один раз за сессию.

- Менеджер **создаёт себя сам** (`[RuntimeInitializeOnLoadMethod]`):
  отдельный persistent-объект, сцена не нужна, синглтон + `DontDestroyOnLoad`.
- Банк `UI` грузится в `Start()`, корутина **поллит** `HasBankLoaded` до 10 с —
  `LoadBank` асинхронный, «одного кадра» мало (урок из
  GlobalAmbienceManager).
- Стартовое состояние принудительно NONE: без этого ивент открывается на
  значении из пресета параметра и один из звуков играет при запуске.
- Одинаковое состояние подряд не переотправляется — FMOD триггерит инструмент
  только на смену значения.
- **Окно видимости 0.1 с**: вспышка параметра в один кадр (16 мс) теряется —
  FMOD-микшер читает параметры чанками ~20 мс. Поэтому состояние держится
  0.1 с и само возвращается в NONE. Звук при этом доигрывает сам — задержка
  не про длину звука.
- Hover ловится **глобальным рейкастом** каждый кадр (`EventSystem.RaycastAll`
  → первая интерактивная `Selectable` под курсором) — покрывает все кнопки
  всех канвас без правки сцен. Клик ЛКМ по кнопке → CLICK.
- `SetVolume` — громкость всего UI одним вызовом.

---

## 9. FmodBankLoader: банки и hot reload

Static-класс, вызывается всеми менеджерами. Зачем он, если RuntimeManager сам
грузит всё из StreamingAssets: явная загрузка со страховкой (`loadSamples:
true`, `BankLoadException` → Warning, не краш), кеш `HashSet` против двойной
загрузки, и главное — **hot reload**.

`GetBanksForEvent(EventReference)` — «в каких банках лежит ивент» (принцип
StudioEventEmitter). Обходной путь, потому что в FMOD 2.03 у EventDescription
**нет** `getBankList`:

```
RuntimeManager.GetEventDescription(reference) → getPath()
RuntimeManager.StudioSystem.getBankList(out Bank[])          // все загруженные банки
  → bank.getEventList(out EventDescription[])
    → совпадение event.getPath() == путь искомого
      → bank.getPath() = "bank:/ИмяБанка" → имя
```

У `Bank` нет `getName` — только `getPath` с префиксом `bank:/`, имя
вырезается. Найденные банки запоминаются в `_eventBanks` — это то, что
выгружает `UnloadEventBanks()`.

**Hot reload** (`AudioZoneManager` → контекстное меню компонента «Reload FMOD
Banks» во время Play): звукорежиссёр пересобрал банки, скопировал в
StreamingAssets — правой кнопкой по компоненту, звук обновляется без
перезапуска сцены. Порядок важен:

1. `ZoneAmbienceSfxScheduler.SilenceAll()` — живые ваншоты ссылаются на
   выгружаемые ивенты;
2. `DisposeZones()` — то же для зональных инстансов;
3. `UnloadEventBanks()` + `UnloadBanks(fmodBanks)` — **Master и Master.strings
   не трогаем** (см. §3);
4. загрузка заново → `BuildZones()` → `RelinkExistingSources()`.

---

## 10. FMOD-проект: папки, ивенты, параметры

Папки ивентов (`Metadata/EventFolder`): `Master` (корень), `AMBIENCE`,
`BUILDINGS`, `GENERATIVE_MUSIC`, `SOUNDTRACK`, `TOWN_DESTINY`, `UI`.

### Ивенты и их код-потребители

| Путь | Банк | Потребитель в коде |
| --- | --- | --- |
| `event:/AMBIENCE/Ambience_Town` | Ambience_Town | AudioZone (50 инстансов) |
| `event:/AMBIENCE/Ambience_Town_SFX_Random` | Ambience_Town | ZoneAmbienceSfxScheduler |
| `event:/AMBIENCE/Ambience_Wind_Generation` | Ambience_Wind_Generator | GlobalAmbienceManager |
| `event:/BUILDINGS/BUILD` | BUILDING | BuildingConstructionAudio |
| `event:/UI/UI` | UI | UiAudioManager |
| `event:/SOUNDTRACK/main_soundtrack` | Main_theme | GameAudio (выключен флагом) |
| `ADAPTIVE_GENERATIVE MUSIC` (папка GENERATIVE_MUSIC) | Main_theme | **кодом пока не управляется** |
| снэпшоты `snapshot:/ESC`, `snapshot:/DEAFULT` | Master | GameAudio (пауза) |

Внутри ивентов живут вложенные события/слои (BED, COMMAND_TRACK, DAY, NIGHT,
CHILL, HAPPY, IMPACT, HQ/LQ/MQ-варианты, Hammer, WOOD, ROCK и т.п.) — в код
они не адресуются напрямую, это внутренности FMOD.

### Параметры, которые код реально шлёт

| Параметр | Кто шлёт | Диапазон | Как |
| --- | --- | --- | --- |
| `Cell_Activity`, `Has_Houses`, `Has_Workshops`, `Has_Market` | AudioZone | 0–1 | `setParameterByName` (в банках пока отсутствуют — см. §5.4) |
| `Distance` (зоны) | AudioZone | 0–100 | `setParameterByName`, каждый тик |
| `Distance` (ветер, глобальный) | GlobalAmbienceManager | 0–1 | `setParameterByID` + `ignoreseekspeed` |
| `BUILDING` | BuildingConstructionAudio | лейблы 0–3 | `setParameterByID` + `ignoreseekspeed` |
| `UI_States` | UiAudioManager | лейблы 0–5 | `setParameterByID` + `ignoreseekspeed` |

Прочие пресеты в проекте (`STRESS`, `MUSIC_STATES`, `DAY-NIGHT`, `Wheel_Scroll`,
`IF_VILLAGE/CULTURE/ENERGY/FACTORY_SECTORE`, `*_DESTINY`) — заготовки под
генеративную музыку и будущие системы.

### Генеративная музыка (ADAPTIVE_GENERATIVE MUSIC)

Один большой ивент в `GENERATIVE_MUSIC` (банк `Main_theme`): 11 дорожек,
вложенные стемы-состояния (DAY/NIGHT, CHILL/HAPPY, HQ/LQ/MQ по уровням
секторов VILLAGE/CULTURE/ENERGY/FACTORY, TOWN_DESTINY_*). Идея последней
итерации: **логика адаптации перенесена из кода в FMOD** — смена стемов и
интенсивности должна решаться automation по параметрам (STRESS, MUSIC_STATES,
IF_*_SECTORE...), а Unity остаётся только выставлять эти параметры.
Статус:FMOD-часть собрана, **код-водитель ещё не подключён** — из Unity этим
параметрам пока никто не пишет. Это следующая большая задача аудио-системы.

---

## 11. Правила API FMOD 2.03 (выстраданные)

Полный список с диагностикой — [[12 Диагностика и грабли]] и AGENTS.md. Здесь —
короткая выжимка «почему код выглядит так, как выглядит»:

- `EventInstance`, `Bank` — **структуры**: `isValid()`, не `== null`;
  сброс — `default`/`clearHandle()`.
- `FMOD.Studio.STOP_MODE.ALLOWFADEOUT` — только полный путь: короткое имя
  конфликтует с `FMODUnity.STOP_MODE`.
- `setParameterByName` — единственный строковый сеттер; глобальный параметр —
  через `RuntimeManager.StudioSystem`, инстансный — через инстанс. Не
  смешивать: у ивента может быть scoped-двойник с тем же именем.
- Из кода управляются только Game Parameter (`parameterType = 0`).
  Автоматические (Distance/Elevation/…, типы 1–9) — READONLY навсегда.
  Unexposed (`isExposedRecursively = false`) — невидимы для API.
- Имена параметров в FMOD держать чистыми: в проекте живут `Distance␣`,
  `BUILDING␣`, `MUSIC_STATES␣` с хвостовыми пробелами — поэтому везде резолв
  по ID с `Trim()` + регистронезависимым сравнением.
- `RuntimeUtils.To3DAttributes` не используется в зонах — позиции
  пересчитываются вручную в `ATTRIBUTES_3D` (зона — не GameObject, трансформа
  нет).
- `bank.getName` не существует; `EventDescription.getBankList` не существует —
  обход через `StudioSystem.getBankList` (см. §9).
- Дамп имён: `(string)desc.name` — прямой вывод даёт `FMOD.StringWrapper`.

---

## 12. Debug-инструменты

- **Гизмо зон** (`AudioZoneManager.showGizmos`, рисуются всегда): зелёный —
  зона видима и звучит; жёлто-зелёный — видима, но FMOD-инстанса нет (пустая);
  красный — мёртвое состояние; сфера — позиция инстанса.
- **Логи**: `LogZoneActivity` (активация/тихая смерть/refresh зон),
  `LogSfxRandom` (выстрелы живчика), `logActivity` у GlobalAmbienceManager
  (readback Distance, дамп глобальных параметров, маршрутизация в шину).
- **Live Update**: FMOD Settings → Live Update (порт 9264) → Play → в FMOD
  Studio `Window → Connect` — уровни дорожек и RTPC в реальном времени;
  `Window → Profiler` — голоса/CPU; Debug Overlay в Settings — оверлей
  играющих ивентов поверх Game View.
- **Hot reload банков**: правой кнопкой по заголовку `AudioZoneManager` →
  Reload FMOD Banks (см. §9).

---

## 13. Устаревшее / на замену

- `FMOD_Cell_Ambience_Design.cs` — историческое ТЗ первой версии (ивент
  `event:/audio/city/Cell_Ambience`, банк `AudioCity`, параметры Day_Night/
  Season/RainIntensity). Путь и банк не существуют; живой ивент —
  `event:/AMBIENCE/Ambience_Town`. Файл оставлен как дизайн-заметка, кодом не
  используется.
- `main_soundtrack` — заменяется генеративной музыкой; флаг `enableMusic`
  включать только для проверки старой темы.
- AGENTS.md местами отстаёт от кода (папки ивентов, дистанционная смерть зон,
  высотный Distance ветра) — при расхождении ориентируйся на этот документ и
  код.

## 14. Дорожная карта (кратко)

1. Параметры плотности `Cell_Activity` / `Has_*` в FMOD Studio + automation на
   слои Ambience_Town — код уже готов, ждёт FMOD.
2. Код-водитель генеративной музыки: проброс STRESS / MUSIC_STATES / IF_*_
   SECTORE из симуляции.
3. Тест производительности: 50 инстансов + живчик против прежних 14 592.
4. Расширение hot reload на банки UI/BUILDING.

Подробный план: [[13 Дорожная карта]].

---

Связанное: [[Home|_Audio]] · [[00 Обзор системы]] · [[../Architecture/06 FMOD Audio|API-справка FMOD]] · [[../Architecture/12 Radial City Grid|Радиальная сетка города]]
