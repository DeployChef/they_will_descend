# 05 Content Pipeline

← [[04 Simulation]] | [[Index]] | Далее → [[06 FMOD Audio]]

Как заводить здания, ресурсы и стартовый город **сейчас**. Не Google Sheet и не blob — ScriptableObject + префаб + SubScene bake.

Связанные: [[12 Radial City Grid]] · [[13 Time HUD and Save]] · [[14 Sim Presentation Bridge]] · [[../Balance/Balance|Balance]]

---

## 1. Карта ролей

Четыре разных объекта. Не мешать.

| Что | Где лежит | Зачем |
| --- | --- | --- |
| **Ключ** | `typeId` / `resourceId` строка | Стык всего: симуляция, HUD, сейв, сценарий, позже таблица |
| **Документ баланса** | `BuildingDefinition` / `ResourceDefinition` | Числа и имя. Не живой дом в сцене |
| **Меш** | префаб с `BuildingAuthoring` | Как выглядит. Не кост, не footprint |
| **Каталог** | `DefaultBuildingCatalog` / `DefaultResourceCatalog` | Список «что существует в этом билде» |
| **Стартовый город** | `ScenarioDefinition` | Какие дома стоят на старте и сколько ресурсов. Не сейв игрока |

Ран после bake:

```text
SO + префаб  →  Baker
                 ↓
session: BuildingPrototype + BuildingCost + ResourceAmount
house stamp: BuildingType (числа типа)
Play: PlaceBuildingCommand.TypeId = "sawmill"
```

Симуляция **не** хранит `GameObject`. Меш для призрака HUD берёт тот же catalog asset на `BuildPlacementController`. Живой дом — entity + Entities Graphics.

---

## 2. Где файлы

```
Assets/_Project/Content/
  Buildings/
    Sawmill.asset
    Kitchen.asset
    DefaultBuildingCatalog.asset
  Economy/
    Wood.asset
    Food.asset
    DefaultResourceCatalog.asset
  Scenarios/
    DefaultScenario.asset
```

Меню создания (ПКМ в Project):

- `They Will Descend / Building Definition`
- `They Will Descend / Building Catalog`
- `They Will Descend / Resource Definition`
- `They Will Descend / Resource Catalog`
- `They Will Descend / Scenario Definition`

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

Сейчас в каталоге: `wood`, `food`.

### Новый ресурс

1. ПКМ в `Content/Economy` → `They Will Descend / Resource Definition`.
2. `resourceId`: `heat` (латиница, snake_case).
3. `displayName`: то, что хочет HUD (`Heat`).
4. Открыть `DefaultResourceCatalog` → добавить ассет в список.
5. Каталог уже назначен на `SimControl` → `ResourceCatalogAuthoring`. Если новый catalog asset — перетащить туда.
6. HUD: чип ищется по **имени дочернего GO** на ResourceBar (`Wood`, `Food`). Новый `Heat` либо получает свободный чип (сейчас лишние — Coal/Steel), либо нужен чип с именем `Heat`.
7. Стартовое количество — **не** на каталоге. Строка в `DefaultScenario` → Starting Stock.

Каталог печёт леджер с amount **0**. Сценарий потом пишет 50 wood / 20 food.

Не клади стартовый запас в `ResourceDefinition`. Это документ типа, не рана.

---

## 5. Здание

Сейчас: `sawmill` (6×2, 15 wood, выпускает wood 1/с) и `kitchen` (2×2, 8 wood, выпускает food 1/с).

### 5.1 Документ

1. ПКМ в `Content/Buildings` → `Building Definition`.
2. Заполнить:

| Поле | Смысл | Срез |
| --- | --- | --- |
| Type Id | ключ | `kitchen` |
| Display Name | HUD / инспектор | `Kitchen` |
| Width Clusters | дуги сетки | как в [[12 Radial City Grid]] |
| Depth Radial Rings | кольца вглубь | обычно 2 |
| Construction Duration | секунды стройки; **0 = взять с CityGrid** (сейчас 8) | 0 ок |
| Workplace Slots | слоты; срез = 1 | 1 |
| Produce Resource | какой `ResourceDefinition` тикает | Wood |
| Produce Per Second | при работающем рабочем | 1 |
| Build Cost | список (ресурс + amount) | 15 Wood |
| Prefab | меш-префаб, см. ниже | |

Кост пустой → дом бесплатный. Несколько строк коста — все должны быть в наличии, списываются вместе.

Выпуск — **заглушка среза**: одно поле, не рецепт. Heat / нужды / несколько выходов — позже, не размазывай второе поле «на всякий».

### 5.2 Префаб

Префаб = **меш**. Цифры на нём не дублировать.

1. Взять модель (или копию существующего `rpgpp_lt_building_*`).
2. На корне: `BuildingAuthoring`.
3. `Definition` = **тот же** `BuildingDefinition`, не соседний дом.
4. В Definition поле Prefab = этот префаб ( circul: SO → prefab → SO ).

Bake падает, если:

- у префаба нет `BuildingAuthoring`;
- authoring смотрит на другой SO;
- prefab пустой;
- `typeId` пустой или дублируется.

Размер меша для посадки на клетку считается с `MeshFilter` (горизонтальный max). Скейлить модель в префабе можно; footprint всё равно с Width/Depth документа.

### 5.3 Каталог

Открыть `DefaultBuildingCatalog` → добавить definition в массив.

Один и тот же asset должен висеть:

1. `SimControl` → `BuildingCatalogAuthoring` (bake, сценарий, HUD-кнопки).
2. Game → `BuildPlacementController` → Catalog (призрак при размещении).

Забыл второй — Play поставит entity, призрак без меша.

Не делай второй catalog «для UI». Один документ.

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
2. `AgentSessionAuthoring` — spawn/assign/unassign + штамп агента (`SimPrototypes`)
3. `CityGridAuthoring` — сетка + `OccupiedCell` + place/reject + `PendingScenarioPlace` + длительность стройки
4. `BuildingCatalogAuthoring` → `DefaultBuildingCatalog`
5. `ResourceCatalogAuthoring` → `DefaultResourceCatalog`

Рядом, **отдельным** GO (не на SimControl: BakingOnly снял бы session singleton):

6. `Scenario` + `ScenarioAuthoring` → `DefaultScenario`
7. HQ (`HeadquarterAuthoring`) — центр сетки, не строка сценария
8. `GameTimeAuthoring`

После смены SO зайди в Play: SubScene перепечётся. Ошибки ключей/префабов — Console при bake, не «тихий нулевой дом».

---

## 8. Что видит игрок

Build HUD читает session-каталог → кнопка с именем и костом (`Sawmill` + `15 Wood`, `Kitchen` + `8 Wood`).

Клик по кнопке каталога → призрак. Красная зона: занято **или** не хватает ресурсов. **ЛКМ** → `PlaceBuildingCommand` без `BuildingId` → симуляция списывает кост, ставит сайт (или сразу дом, если duration уже 0). После `Playback()` режим **остаётся**, если ещё хватает ресурса. **ПКМ** / **Esc** — отмена.

Сценарий и load (`BuildingId > 0` или `InstantComplete`) кост не берут.

Производство: готовый дом, не стройка, не HQ, на слоте есть рабочий и `Working`. Тикает `Produce Resource` × dt.

Инспектор дома берёт **Display Name из каталога**, не `Building_17`.

Сейв пишет `"sawmill"` / `"wood"` как есть. Старые слоты не мигрируем: несовпадение версии удаляет файл. Подробно — [[13 Time HUD and Save]].

---

## 9. Типичные поломки

| Симптом | Что проверить |
| --- | --- |
| Console: duplicate typeId | Два SO с одним ключом в catalog |
| Console: prefab must have BuildingAuthoring pointing at … | Забыл компонент или SO на префабе ≠ документ в каталоге |
| HUD пустой / «catalog empty» | SubScene не запеклась; catalog не на SimControl |
| Призрак без меша, дом после клика есть | `BuildPlacementController.Catalog` не тот asset |
| Стартовый запас 0 | Запас на Scenario, не на ResourceDefinition; Scenario GO есть? |
| Игрок ставит бесплатно | Пустой Build Cost на definition |
| Сценарий съел дерево | Не должно: InstantComplete. Если ест — сломан skip в Place |
| Capture обнулил Wood | Не должно: Capture пишет только buildings |
| Новый ресурс не на HUD | Имя чипа ≠ Display Name; свободных чипов нет |
| Дом не того размера на сетке | Width/Depth на SO, не скейл префаба. Скейл только вписывает меш в клетку |

---

## 10. Google Sheet — потом, не сейчас

Сейчас цифры и префаб на одном `BuildingDefinition`. Для двух домов так и надо.

Таблица **не** хранит Unity-ссылку. Когда вынесете баланс:

```text
Sheet  →  typeId, footprint, cost, produce
Unity registry  →  typeId → Prefab (иконка, FMOD)
Baker склеивает по typeId
```

Не пишите в ячейку `Assets/…/house.prefab`. Ключ уже строка — стык готов. Резать SO на два файла **до** импорта Sheet не нужно.

---

## 11. Контрольный прогон нового дома

1. SO + префаб с `BuildingAuthoring` на этот SO + строка в `DefaultBuildingCatalog`.
2. Play без ошибок duplicate / missing prefab.
3. В Build HUD есть кнопка с именем и костом.
4. Призрак садится на сетку нужного размера.
5. Постановка списывает wood; при нехватке — красный призрак и reject.
6. Сценарий с этим типом (если добавил) ставит дом без списания.
7. Рабочий на готовом доме тикает выбранный ресурс.

Не канон среза (не чини «на всякий»): один выход вместо рецепта; HUD-чипы ресурсов заведены сценой, не спавнятся из каталога.

---

Связанные: [[01 Folder Structure]] · [[08 Production ECS]] · [[../GDD/10 Roadmap|Roadmap]]
