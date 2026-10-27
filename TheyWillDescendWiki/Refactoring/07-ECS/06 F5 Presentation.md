# 06. Ф5 — Чтение в Presentation

← [[05 F4 Inputs]] | [[Index]]

После Ф2 панели могут держать `Entity`. После Ф3 есть `WorkforceSummary`. Ф5 убирает ежекадровые сканы мира из HUD.

Виды (`BuildingViewBoard`, `AgentViewBoard`) мир сканировать **могут** — это их работа (синхронизация GO). Не путать с HUD.

---

## 1. Что убрать

| Место | Сейчас | Станет |
| --- | --- | --- |
| `BuildingInspectPanel.Update` | `TryFindBuilding` по id, 2–3 query workforce/crew/time, `FindAnyObjectByType` | читать `selection.Selected`; `em.Exists`; компоненты дома; сводка; время с сессии/`SimWorld` |
| `PyramidInspectPanel.Update` | `CreateEntityQuery<Headquarters>` каждый кадр | `CityCore.Headquarters` с сессии |
| `BuildingInspectPanel.CountWorkforce` | все агенты + сумма Desired | `WorkforceSummary` |
| `CountConstructionCrew` | проход `AgentAssignment` | поля на `Construction` (см. ниже) |
| `BuildingSelection._buildingQuery` на MB | кэш query с чужим временем жизни | не кэшировать на GO; для пика по сектору — lookup/временный query в методе клика, не в Update |
| `FindAnyObjectByType<BuildPlacementController>` в Update | | ссылка с инспектора / VContainer Game scope (как `BuildingViewBoard.Construct`) |
| `EnergyFillImage` → `FindAnyObjectByType<OrbEnergyGlow>` | | инспектор |
| Строки HUD каждый кадр | `$"{x} / {y}"` | писать Text только если значение изменилось |

---

## 2. Read model, который должна писать симуляция

Уже есть после Ф3: `WorkforceSummary`.

Добавить:

### CityCore на сессии

```csharp
public struct CityCore : IComponentData
{
    public Entity Headquarters;
}
```

Пишет bootstrap в Ф1 можно сразу — если не сделали, сделать здесь. UI: `em.GetComponentData<CityCore>(bag).Headquarters`.

### Счётчики бригады на Construction

Чтобы HUD не сканировал агентов:

```csharp
// поля на Construction (или отдельный компонент на том же entity)
public int CrewAssigned;
public int CrewArrived;
```

Пишет `ClaimConstructionCrewSystem` (он и так знает, кто claimed). Инспектор читает с выбранного дома.

Не заводить второй глобальный query «все стройки».

---

## 3. Запросы не на MonoBehaviour

`SimWorld` живёт столько же, сколько World. Если query всё же нужен (доски видов) — кэш на `SimWorld` или локально в `LateUpdate` с проверкой `world.IsCreated` и сбросом в `OnDestroy` (как сейчас view boards — приемлемо).

Запрещено: `CreateEntityQuery` в `Update` виджета HUD. `using var query` на каждый кадр в инспекторе — удалить.

`TimeWidget` / `TimeLightController` / `Story*`: если берут `GameTime` через новый query каждый кадр — перевести на `SimWorld` + `GetComponentData<GameTime>(bag)` (время на сессии после Ф1). Это O(1) без query.

`CheatButtons` пишет `GameTime` — ок как чит; query можно заменить на bag.

`ResearchWidget` query `TechInfo` — список карт. Это не HUD-карточка одного entity; допустим query при открытой панели, не каждый кадр если панель закрыта. Если открыта — лучше кэш entity карт после Populate (их набор за ран почти не меняется). Не обязательно в Ф5, если профайлер тихий.

---

## 4. AgentViewBoard — сначала измерить

`Allocator.Temp` ≠ GC. Пять массивов на 100 агентов могут быть незаметны.

Порядок:

1. Profiler на ~200 агентах (Debug-сценарий + спавн): время `AgentViewBoard.LateUpdate`, `GC.Alloc`.
2. Если &lt; 0.2 мс и 0 B GC — **не трогать**, записать цифры в этот файл и в 07.
3. Если больше — один проход по чанкам (`foreach` Query вместо пяти `ToComponentDataArray`), не «пулы на будущее».

Не начинать Ф5 с переписывания доски «потому что документ 03 сказал микрофризы». Документ 03 не измерял.

---

## 5. Порядок работ

1. `CityCore` + заполнение в bootstrap. Пирамида-инспектор без query.
2. Поля crew на `Construction`, пишет Claim. Инспектор дома без CountConstructionCrew.
3. Инспектор дома: только Selected Entity + WorkforceSummary + GameTime с bag.
4. Ссылки на соседей через инспектор/Inject, не Find в Update.
5. Строки — кэш предыдущего значения (низкий приоритет, вместе с Show).
6. Измерить AgentViewBoard, решить.
7. Grep.

Не менять layout HUD, не «улучшать» тексты.

---

## 6. Grep-ворота

```
CreateEntityQuery     в Presentation/GameHud/   — пусто
FindFirstObjectByType / FindAnyObjectByType     в Update/LateUpdate — пусто
TryFindBuilding       — нет
CountWorkforce        — нет
```

Профайлер: открытая карточка дома без движения значений — 0 B GC от панели.

---

## 7. Вики после Ф5

- [[../../Architecture/14 Sim Presentation Bridge|14]] — pull без query в HUD
- [[../../Architecture/03 Presentation & Performance|03]] в рефакторинге — результат замера AgentViewBoard
- [[../07 Foundation Fixes|07]] §7
