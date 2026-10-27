# 03. Ф2 — Идентичность на Entity

← [[02 F1 Run World]] | [[Index]] | далее [[04 F3 Workforce]]

Делать **только после Ф1**. Иначе restore снова гоняет id через запросы.

Цель: геймплейные ссылки — `Entity`. `int Id` остаётся ключом JSON и стабильного порядка, не адресом в мире.

---

## 1. Было → станет (поля)

### AgentAssignment

```text
БЫЛО                              СТАНЕТ
WorkplaceBuildingId : int         Workplace : Entity   // Null = свободен
ConstructionBuildingId : int      ConstructionSite : Entity
Arrived : byte                    Arrived : byte       // без изменения смысла
HasConstructionTask               ConstructionSite != Entity.Null
```

Переписать хелперы `CanKeepConstruction` / `IsFreeForConstruction` на Entity. Сравнения `== 0` → `== Entity.Null`.

### OccupiedCell

```text
BuildingId : int   →   Building : Entity
```

### RoadSegment

```text
SiteId : int       →   Site : Entity
```

`RoadSegment.Id` (номер сегмента для UI сноса дороги) можно оставить int — это не ссылка на дом, а id самого ребра. `DemolishRoadRequest.SegmentId` пока int. Не смешивать с Building.Id.

### DemolishBuildingRequest

```text
BuildingId : int   →   Building : Entity
```

UI передаёт entity выбранного дома (после смены `BuildingSelection`).

### SpawnHouse / occupancy

При спавне в каждую `OccupiedCell` писать `Building = entity`, не id.

### CityGrid.NextBuildingId

Перенести генератор в `BuildingIdSequence` на сессии (как `AgentIdSequence`). Сетка не хранит идентификаторы. `Building.Id` по-прежнему выдаётся при спавне для сейва.

---

## 2. Правило висячих ссылок

Когда дом уничтожен, `Entity` у агентов становится invalid (версия). **Никто не обходит всех агентов, чтобы обнулить id.**

Система, которая идёт по ссылке:

```csharp
var workplaces = SystemAPI.GetComponentLookup<Workplace>(true);
var transforms = SystemAPI.GetComponentLookup<LocalTransform>(true);

var target = assignment.ValueRO.Workplace;
if (target == Entity.Null) continue;
if (!workplaces.HasComponent(target))
{
    assignment.ValueRW.Workplace = Entity.Null;
    assignment.ValueRW.Arrived = 0;
    continue;
}
```

`HasComponent` / `ComponentLookup.HasComponent` — O(1), Burst. Это самоисцеление desired state: сломанное состояние лечится следующим тиком.

Тонкость: дом может жить, но быть в `Construction` (стройка или снос). Для **работы на станке** недостаточно `Exists`. Нужно: есть `Workplace` **и** нет `Construction`. Для **ходьбы на стройку** — есть `Construction`. Для **снятия occupancy** — сущность уже уничтожена, клеток с её Entity быть не должно (их снимает `BuildingDismantle.Complete` до Destroy).

`BuildingDismantle`:

- `Begin(em, Entity building)` — без `TryGetBuilding`.
- `ReleaseCrew` — **удалить**. Агенты с `ConstructionSite == destroyed` сбросятся в Claim/Commute на следующем тике.
- В `Begin` сейчас обнуляются `WorkplaceBuildingId` у всех агентов дома и `DesiredWorkers = 0`. После Ф2: не обходить агентов. На дом повешен `Construction` (снос) → диспетчер (Ф3) и commute видят «не Workplace для работы». До Ф3 диспетчер ещё смотрит `WithNone<Construction>()` — уже не назначает на этот дом; старые назначения: **надо решить явно в этом шаге**:

**Решение Ф2:** в `Begin` по-прежнему можно сбросить `Workplace = Null` у агентов этого дома, но искать их запросом `AgentAssignment` где `Workplace == entity`, не линейным поиском дома. Это не «очистка всех id мира», это «этот дом больше не рабочее место». Альтернатива — оставить ссылку, диспетчер в Ф3 отпустит, потому что дом в Construction / уничтожен. Предпочтительнее **не** полный проход: `foreach assignment where Workplace == site`. Если агентов мало — ок. Не делать `TryGetBuilding`.

`Complete` уничтожает entity; occupancy снимать **до** Destroy, сравнивая `OccupiedCell.Building == site`.

---

## 3. Системы: убрать HashMap&lt;int&gt; и линейный поиск по id

| Система | Сейчас | После |
| --- | --- | --- |
| `WorkforceDispatchSystem` | `NativeHashMap<int,int>` по BuildingId | группировать по `Entity` Workplace; в Ф3 перепишется логика, в Ф2 только ключ |
| `SyncWorkplaceLoadSystem` | `NativeHashMap<int, Entity>` | писать прямо в `Workplace` по `assignment.Workplace`, если `HasComponent` |
| `AdvanceAgentCommuteSystem` | `NativeList<HouseRef>{Id, Pos}` + `TrySteerTo` линейно | `ComponentLookup<LocalTransform>` по `ConstructionSite` / `Workplace` |
| `ClaimConstructionCrewSystem` | `Site.Id` + `FindSite` линейно | список площадок как `Entity` + позиция; поиск «есть ли ещё эта площадка» — lookup `Construction`. Выбор ближайшей среди площадок — остаётся, это не поиск по id |
| `BuildingDismantle.TryGetBuilding` | полный проход | удалить |
| `BuildingInspectPanel.TryFindBuilding` | полный проход | Ф5. В Ф2 достаточно, чтобы панель компилировалась: либо временно искать по `Building.Id` (ещё жив), либо уже держать Entity из selection. **Минимум Ф2:** `BuildingSelection` отдаёт Entity, инспектор читает его. Иначе Ф2 не соберётся, если снесёте `SelectedBuildingId` |

Presentation **в том же PR**, иначе CS0535/CS1061.

---

## 4. Presentation в Ф2

### BuildingSelection

```text
SelectedBuildingId : int   →  Selected : Entity
HoveredBuildingId          →  Hovered : Entity
SelectBuilding(int)        →  SelectBuilding(Entity)
ClearIf(int)               →  ClearIf(Entity)
```

Перед использованием `em.Exists(Selected)`. Клик по мешу: `BuildingIdTag` несёт `Entity`, не int.

### BuildingIdTag

```text
Id : int  →  Entity Target  (или добавить Entity, int убрать)
```

`BuildingViewBoard.CreateHouseView` пишет `tag.Target = entity`.

Словарь видов `_views[building.Id]` **можно оставить на int** — это ключ пула/сейва, не ссылка симуляции. Не обязательно переходить на Entity как ключ словаря.

### AgentViewBoard

Ключ `AgentId` для видов — ок (скин Mixamo, сейв). `AgentAssignment` больше не читается доской для поиска дома.

### Кто ещё читает int-ссылки

Grep перед стартом фазы:

```
WorkplaceBuildingId
ConstructionBuildingId
SelectedBuildingId
HoveredBuildingId
SiteId
\.BuildingId
```

Каждое место — либо сейв (`AgentSnapshot.workplaceBuildingId`), либо порядок (`SortById`), либо переводится на Entity.

---

## 5. Сейв

JSON **не** хранит Entity (индекс+версия бессмысленны между ранами).

```text
Capture:  assignment.Workplace → Building.Id этого entity (0 если Null/мёртв)
Restore:  спавн домов (карта id→Entity, уже есть в Ф1)
          агент.Workplace = map[snapshot.workplaceBuildingId] или Null
```

Это единственные два места, где разрешён поиск/маппинг по `Building.Id`.

Опционально в Ф2 (если файл snapshot и так открыт): добавить `constructionBuildingId` в `AgentSnapshot` и бамп версии. Не обязательно, но дыра известная. Не делать, если не хотите трогать версию слота.

---

## 6. Порядок работ

1. Grep всех потребителей полей из §1. Составить список файлов (Simulation + Presentation).
2. Сменить структуры (`AgentAssignment`, `OccupiedCell`, `RoadSegment`, `DemolishBuildingRequest`). Проект не соберётся — это ожидаемо.
3. Починить Simulation: Spawn, dismantle, commute, claim, dispatch (только ключ), sync load, production не должен смотреть id.
4. Починить Presentation: tag, selection, inspect (чтение entity), snapshot capture/restore.
5. `BuildingIdSequence` вместо `CityGrid.NextBuildingId`.
6. Grep-ворота. Play: снос дома с назначенными — люди на плазе за тик, без NRE. Сейв/загрузка — те же дома.

Не входить в логику «Paused отпускает» — Ф3. После Ф2 диспетчер всё ещё может писать `DesiredWorkers` и desired=0 на паузе; ключ только Entity.

---

## 7. Узкие места

**Сравнение Entity.** `== Entity.Null` безопасно. `== otherEntity` после Destroy левого — false, потому что версия. Не хранить id «на всякий случай» рядом «пока не уверены».

**Burst + EntityManager.Exists.** В `ISystem` Burst-пути использовать `ComponentLookup.HasComponent`, не `em.Exists`.

**Дороги.** `RoadViewBoard` / `DemolishRoadRequest` сидят на `RoadSegment.Id`. Не переименовывать в Site. Смена только `SiteId → Site` для связи с площадкой. Проверить `BuildingDismantle.RemoveRoad`: сравнивать `Site == entity`.

**ClaimConstructionCrew SortById.** После перехода сортировать площадки по `Building.Id` с lookup `Building` (стабильность Ф0.3) или по `Entity.Index` (не стабилен между сейвами). Для claim порядок площадок — «кто первый забирает слот»; для **диспетчера штата** стабильность важнее. В Ф2 claim может сортировать по `Building.Id` через lookup. Не сортировать по Index.

**Инспектор и клик в одном кадре.** Selection пишет Entity, дом сносят — инспектор должен `Exists` и закрыться. Сейчас `ClearIf(id)` зовёт view board при destroy. Сохранить этот вызов с Entity.

**Headquarters.** У HQ нет `Building`. Не класть HQ в `OccupiedCell`. Клик пирамиды — по-прежнему `PyramidView` на GO.

---

## 8. Grep-ворота

Пусто в игровом коде (не в snapshot DTO):

```
WorkplaceBuildingId
ConstructionBuildingId
SelectedBuildingId
HoveredBuildingId
SiteId
```

По `Simulation/`:

```
\.Id !=
\.Id ==
```

допустимы только: сравнение `Building.Id` в snapshot restore/capture, сортировка по ключу, `RoadSegment.Id`, `AgentId.Value`.

`TryGetBuilding` — нет.

---

## 9. Критерии успеха

- Снос дома с рабочими: за тик люди без NRE на плазе, производство дома исчезло.
- Сейв → загрузка: рабочие идут в те же дома (по Building.Id в JSON → Entity).
- Стройка продолжается с того же `Elapsed` (это Ф1 spawn Construction; Ф2 не ломает).
- Нет линейного поиска дома по id в тике.

---

## 10. Вики после Ф2

- [[../../Architecture/04 Simulation|04]] — ссылки Entity
- [[../../Architecture/14 Sim Presentation Bridge|14]] — selection Entity
- `.cursor/rules/ecs-production-architecture.mdc` — «поиск по int Id запрещён в геймплее»
- [[../07 Foundation Fixes|07]] §4
