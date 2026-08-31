# 05 Content Pipeline

← [[04 Simulation]] | [[Index]] | Далее → [[06 FMOD Audio]]

Как заводить здания, ресурсы и стартовый город **сейчас**. Не Google Sheet и не blob. Дом = префаб с модулями authoring. Ресурс / правила мира / эры — ScriptableObject.

Связанные: [[12 Radial City Grid]] · [[13 Time HUD and Save]] · [[14 Sim Presentation Bridge]] · [[../Balance/Balance|Balance]]

---

## 1. Карта ролей

| Что | Где лежит | Зачем |
| --- | --- | --- |
| **Ключ** | `typeId` / `resourceId` строка | Стык всего: симуляция, HUD, сейв, сценарий, позже таблица |
| **Дом (тип)** | префаб: `BuildingKey` + модули + куб/меш + `BuildingView` | состав, цифры, вид. Нет второго SO «карточка лесопилки» |
| **Ресурс / правила / эры** | `ResourceDefinition` / `SimRules` / `TimelineCatalog` | не пространственные документы |
| **Каталог домов** | `DefaultBuildingCatalog` — список **префабов** | что можно строить в этом билде |
| **Стартовый город** | `ScenarioDefinition` | какие ключи стоят на старте. Не сейв игрока |

Ран после bake:

```text
префаб  →  Baker
             ↓
session: BuildingPrototype (typeId → entity штампа)
штамп: BuildingType + optional Workplace + recipe/cost buffers
Play: PlaceBuildingCommand.TypeId = "sawmill"  →  Instantiate(штамп)
```

Имя кнопки и цвет куба HUD берёт с `BuildingView` на том же префабе (не из ECS). Призрак — тот же catalog asset на `BuildPlacementController`. Живой дом — entity + Entities Graphics.

Sheet позже пишет **цифры** в поля модулей по `typeId`. Состав (есть Workplace или нет) остаётся на префабе. Импортёра нет.

---

## 2. Где файлы

```
Assets/_Project/Content/
  Buildings/
    DefaultBuildingCatalog.asset
    Prefabs/
      _BuildingStamp.prefab   ← шаблон, не в каталоге
      Kitchen.prefab
      Sawmill.prefab
  Economy/
    Wood.asset
    Food.asset
    DefaultResourceCatalog.asset
  Scenarios/
    DefaultScenario.asset
    DebugScenario.asset
  Rules/
    DefaultSimRules.asset
```

Меню создания (ПКМ в Project):

- `They Will Descend / Building Catalog`
- меню `They Will Descend / Buildings / Create Cube Stamps` — один раз создаёт куб-штампы и пишет их в каталог
- `They Will Descend / Resource Definition`
- `They Will Descend / Resource Catalog`
- `They Will Descend / Scenario Definition`
- `They Will Descend / Sim Rules`

Сцены:

- Каталоги на **SimControl** в `Assets/_Project/SubScenes/Simulation.unity`
- Тот же **Building Catalog** на `BuildPlacementController` в Game (призрак)
- Сценарий — отдельный GO `Scenario` в той же SubScene, **не** на SimControl

---

## 3. Правила ключа

`typeId` и `resourceId` — человеческие id, не счётчики.

Хорошо: `sawmill`, `kitchen`, `wood`, `food`.  
Плохо: `1`, `House 6x2`, `Wood`.

Bake приводит ключ к **lowercase** и обрезает пробелы по краям. Пустое поле → имя ассета (тоже lowercase). Длиннее ~61 байт UTF-8 — ошибка.

Дубликат ключа в одном каталоге — ошибка в Console, строка не печётся.

`Building.Id` (1, 2, 3…) — это **номер дома в этом ране**, не тип. Тип всегда строка.

---

## 4. Ресурс

Сейчас в каталоге: `wood`, `food`, `energy`.

Энергия — ресурс (`canFeed` выкл: слайдер пирамиды её не жжёт). `energyValue` — сколько энергии даёт единица при сжигании на пирамиде. `stockCap` 0 = взять `SimRules.DefaultStockCap` (временный потолок до складов).

### Новый ресурс

1. ПКМ в `Content/Economy` → `They Will Descend / Resource Definition`.
2. `resourceId`: `heat` (латиница, snake_case).
3. `displayName`: то, что хочет HUD (`Heat`).
4. Открыть `DefaultResourceCatalog` → добавить ассет в список.
5. Каталог уже назначен на `SimControl` → `ResourceCatalogAuthoring`. Если новый catalog asset — перетащить туда.
6. HUD: чип ищется по **имени дочернего GO** на ResourceBar (`Wood`, `Food`). Новый `Heat` либо получает свободный чип (сейчас лишние — Coal/Steel), либо нужен чип с именем `Heat`.
7. Стартовое количество — **не** на каталоге. Строка в `DefaultScenario` → Starting Stock.

Каталог печёт леджер с amount **0**. Сценарий потом пишет стартовый запас (сейчас 1000 wood / 1000 food).

Не клади стартовый запас в `ResourceDefinition`. Это документ типа, не рана.

---

## 4b. Правила мира (`SimRules`)

Длина суток, смена 6–18, скорость ходока — **не** сценарий и не тип дома.

Ассет: `Content/Rules/DefaultSimRules`. На `SimControl` висит `SimRulesAuthoring` → тот же ассет.

Baker копирует:

| Поле SO | Куда в ECS |
| --- | --- |
| Day Duration | `GameTime.DayDuration` (на session) |
| Work Shift Start/End | `GameTime.WorkShiftStartHour` / `EndHour` |
| Worker Speed | штамп `AgentLocomotion.Speed` |
| Era Change Hour | `PyramidConfig.EraChangeHour` (граница эры, не полночь) |
| Pyramid Max Energy / Hour | `PyramidConfig.MaxEnergyPerHour` (потом обелиски) |
| Default Stock Cap | временный потолок стока |

На том же GO: `TimelineCatalogAuthoring` → `DefaultTimeline` (эры, дань, max loyalty).

Системы SO не читают. Play после правки ассета перепечёт SubScene.

---

## 5. Здание

Документ типа — **префаб**. Куб сейчас (заглушка под меш GD). Цвет куба: стройка / работает / стоит — Presentation, не система тика.

Срез после `Create Cube Stamps`: `sawmill` (6×2, 15 wood, +12 Wood/ч) и `kitchen` (2×2, 8 wood, −6 Wood/ч → +12 Food/ч). RPGPP-меши в пакете оставлены, со штампов сняты.

### 5.1 Новый дом (процесс ГД)

Не копировать кухню и «вычищать». Базовый жест — duplicate **`_BuildingStamp`**, затем **добавить** модули.

1. Duplicate `_BuildingStamp` → `Factory`. Сразу `BuildingKey.typeId = factory` (уникальный, lowercase).
2. Нужны люди — `BuildingWorkplaceAuthoring` (слоты).
3. Нужно варить — `BuildingRecipeAuthoring` (те же `ResourceDefinition`).
4. Платный — `BuildingCostAuthoring`. Долгая стройка — `BuildingConstructionAuthoring`.
5. `BuildingView` — display name, цвета куба. Не печётся в ECS.
6. Префаб в `DefaultBuildingCatalog` (список префабов).
7. Play: кнопка, призрак = этот префаб, Place = `Instantiate` штампа.

Кухня → похожая кухня: duplicate `Kitchen`, сменить `typeId` первым. Склад: duplicate штампа, не кухни. HQ / пирамида **не** в этом каталоге.

Цифры экономики — поля модулей на префабе. Sheet позже перезапишет те же поля по ключу; состав модулей таблица не создаёт.

### 5.2 Модули на корне

| Компонент | Смысл | Нет модуля = |
| --- | --- | --- |
| `BuildingKey` | `typeId` | bake падает |
| `BuildingFootprintAuthoring` | кластеры × кольца | bake падает |
| `BuildingConstructionAuthoring` | секунды; 0 / нет = сразу готовый | мгновенно |
| `BuildingWorkplaceAuthoring` | слоты | HUD без +/−, production не ищет рабочих |
| `BuildingRecipeAuthoring` | in/out за игровой час | не варит |
| `BuildingCostAuthoring` | списание при Place | бесплатно |
| `BuildingView` | имя, цвета | HUD показывает `typeId` |

Bake падает, если нет Key / Footprint, пустой или слишком длинный `typeId`, дубликат ключа в каталоге.

Рецепт живёт **на штампе** (буфер instance). Симуляция: `perHour * dt * 24 / DayDuration`. Размер меша для посадки — `MeshFilter` при bake (`BuildingMeshSize`).

### 5.3 Каталог

Открыть `DefaultBuildingCatalog` → массив префабов (не SO-карточек).

Один и тот же asset:

1. `SimControl` → `BuildingCatalogAuthoring`.
2. Game → `BuildPlacementController` → Catalog.

Забыл второй — Play поставит entity, призрак без меша. Не делай второй catalog «для UI».

---

## 6. Сценарий (стартовый город)

`DefaultScenario` — пустой/рабочий старт. `DebugScenario` — оба дома (Kitchen + Sawmill) на кольце 2 и 8 рабочих на плазе. Сейчас на GO `Scenario` висит debug. Сейв игрока — [[13 Time HUD and Save]].

На ассете:

- **Buildings** — список `(typeId, cluster, ring)`. HQ и сетка сюда не входят.
- **Starting Stock** — `Wood 50`, `Food 20`. Capture домов **не** затирает это.
- **Starting Workers** — сколько людей на плазе в Play. Не назначение на дома. Capture домов это тоже не трогает. По умолчанию 8.

### Editor на GO Scenario (SubScene)

Нужны в SubScene: `CityGridAuthoring`, `BuildingCatalogAuthoring`, `HeadquarterAuthoring`.

| Кнопка / инструмент | Что делает |
| --- | --- |
| Add building (по типам каталога) | Пишет строку в SO + ставит превью на первую свободную клетку |
| Scene tool **Place scenario buildings** | Пока выбран GO `Scenario`: тип в палитре Scene view, ЛКМ по сетке → клетка в SO. ПКМ по превью → убрать |
| Apply config → scene | Стирает превью, спавнит заново из SO |
| Capture scene → config | Превью → список домов в SO. Запас не трогает |
| Move tool | Тащишь превью — snap в клетку, MouseUp пишет SO |

Bake сценария: starting stock в леджер; дома — `PendingScenarioPlace`, люди — `PendingScenarioSpawns` на session. Первый тик **Play** делает Place/Spawn. В bake **нельзя** Instantiate штампа каталога: Live Conversion даёт DuplicateEntityGuid. Превью в SubScene unpack’аются полностью (не prefab instance).

`ScenarioAuthoring` нельзя вешать на SimControl: BakingOnly снял бы session singleton.

Overlap на сетке: Inspector красный, bake лишние дома reject'нет в Console.

---

## 7. Session authoring (чеклист SubScene)

На **одном** GO `SimControl` (соседи authoring, один bake-entity):

1. `SimControlAuthoring` — `SimControl` + `SimBridge` + буфер `SimClockCommand`
2. `SimRulesAuthoring` → `DefaultSimRules` (сутки, смена, скорость ходока, час эры, потолок жжения, cap стока)
3. `TimelineCatalogAuthoring` → `DefaultTimeline`
4. `AgentSessionAuthoring` — spawn/assign/unassign + штамп агента (`SimPrototypes`)
5. `CityGridAuthoring` — сетка + `OccupiedCell` + place/reject + `PendingScenarioPlace`
6. `BuildingCatalogAuthoring` → `DefaultBuildingCatalog`
7. `ResourceCatalogAuthoring` → `DefaultResourceCatalog`

Рядом, **отдельным** GO (не на SimControl: BakingOnly снял бы session singleton):

8. `Scenario` + `ScenarioAuthoring` → `DefaultScenario`
9. HQ (`HeadquarterAuthoring`) — центр сетки, не строка сценария

После смены SO зайди в Play: SubScene перепечётся. Ошибки ключей/префабов — Console при bake, не «тихий нулевой дом».

---

## 8. Что видит игрок

Build HUD: ключи из `BuildingPrototype`, имя с `BuildingView` на префабе, кост с буфера штампа.

Клик по кнопке каталога → призрак. Красная зона: занято **или** не хватает ресурсов. **ЛКМ** → `PlaceBuildingCommand` без `BuildingId` → симуляция списывает кост, ставит сайт (или сразу дом, если duration уже 0). После `Playback()` режим **остаётся**, если ещё хватает ресурса. **ПКМ** / **Esc** — отмена.

Сценарий и load (`BuildingId > 0` или `InstantComplete`) кост не берут.

Производство: готовый дом с `Workplace` + своим `BuildingRecipeLine`, не стройка, не HQ, `WorkingCount > 0`. Единица — игровой час.

Инспектор: имя с `BuildingView`, слоты с `BuildingType` / `Workplace` на entity.

Куб зелёный, когда `WorkingCount > 0`; жёлтый на стройке; иначе idle. Потом те же флаги → Animator.

Сейв пишет `"sawmill"` / `"wood"` как есть. Старые слоты не мигрируем: несовпадение версии удаляет файл. Подробно — [[13 Time HUD and Save]].

---

## 9. Типичные поломки

| Симптом | Что проверить |
| --- | --- |
| Console: duplicate typeId | Два префаба с одним ключом в catalog |
| Console: needs a BuildingKey / Footprint | На префабе нет обязательных модулей |
| HUD пустой / «catalog empty» | Не гоняли Create Cube Stamps; SubScene не запеклась; catalog не на SimControl |
| Сутки снова 5 с / нет смены | `SimRulesAuthoring` без ассета; править `DefaultSimRules`, не Inspector SubScene |
| Призрак без меша, дом после клика есть | `BuildPlacementController.Catalog` не тот asset |
| Стартовый запас 0 | Запас на Scenario, не на ResourceDefinition; Scenario GO есть? |
| Игрок ставит бесплатно | Нет `BuildingCostAuthoring` (или пустой список) |
| Сценарий съел дерево | Не должно: InstantComplete. Если ест — сломан skip в Place |
| Capture обнулил Wood | Не должно: Capture пишет только buildings |
| Новый ресурс не на HUD | Имя чипа ≠ Display Name; свободных чипов нет |
| Дом не того размера на сетке | Width/Depth на Footprint, не скейл куба. Скейл только вписывает меш в клетку |

---

## 10. Google Sheet — потом, не сейчас

Цифры дома — поля модулей на префабе. Стык Sheet уже есть: `typeId`.

Таблица **не** хранит Unity-ссылку. Когда вынесете баланс:

```text
Sheet  →  typeId, footprint, cost, recipe in/out per hour
Unity registry  →  typeId → Prefab (меш, иконка, FMOD, BuildingView)
Baker склеивает по typeId
```

Импорт **перезаписывает числа**, не добавляет `Workplace` строкой «yes». Нет модуля — ошибка, не молчаливый `slots = 0`.

Не пишите в ячейку `Assets/…/house.prefab`. Импортёра в этом срезе нет.

---

## 11. Контрольный прогон нового дома

1. Меню `They Will Descend / Buildings / Create Cube Stamps` (или duplicate `_BuildingStamp` + модули + строка в каталоге).
2. Play без ошибок duplicate / missing BuildingKey.
3. В Build HUD есть кнопка с именем и костом.
4. Призрак-куб садится на сетку нужного размера; цвет меняется, когда дом варит.
5. Постановка списывает wood; при нехватке — красный призрак и reject.
6. Сценарий с этим типом (если добавил) ставит дом без списания.
7. Рабочий на готовом доме варит по рецепту (кухня без wood стоит).

Не канон среза (не чини «на всякий»): HUD-чипы ресурсов заведены сценой, не спавнятся из каталога.

---

Связанные: [[01 Folder Structure]] · [[08 Production ECS]] · [[../GDD/10 Roadmap|Roadmap]]
