# 05. Ф4 — Входы игрока

← [[04 F3 Workforce]] | [[Index]] | далее [[06 F5 Presentation]]

После Ф1 очередь setup-команд уже мертва. Ф4 наводит порядок на **оставшихся** входах.

**Слайдеры пирамиды не трогать** (решение Ф0.4). `SetPyramidFeedRequest` остаётся request'ом до нового дизайна.

---

## 1. Канон папок внутри фичи

```text
Simulation/<Feature>/
  Components/        состояние, поля желания
  Requests/          IComponentData одноразовых сущностей
  RequestSystems/    Process*RequestsSystem
  Commands/          только буферы-потоки и outbound-события
  Systems/           тик
```

Сейчас третье: `CommandSystems/` со смесью Consume* для request **и** для буферов. Целевое:

| Было | Станет | Тип входа |
| --- | --- | --- |
| `ConsumePlaceBuildingCommandsSystem` | `ProcessPlaceBuildingRequestsSystem` | request |
| `ConsumeSpawnAgentCommandsSystem` | `ProcessSpawnAgentRequestsSystem` | request |
| `ConsumeDeconstructBuildingCommandsSystem` | `ProcessDemolishBuildingRequestsSystem` | request |
| `ConsumePlaceRoadStrokeSystem` | `ProcessPlaceRoadStrokeRequestsSystem` | request |
| `ConsumeDemolishRoadSystem` | `ProcessDemolishRoadRequestsSystem` | request |
| `ConsumeSetActiveResearchCommandsSystem` | `ProcessSetActiveResearchRequestsSystem` | request |
| `ConsumeSetPyramidFeedCommandsSystem` | **не переименовывать в желании**; можно переименовать в `ProcessSetPyramidFeedRequestsSystem` для единообразия имени, канал не менять | request (временно) |
| `ConsumeSimClockCommandsSystem` | остаётся в `CommandSystems/` (буфер) | буфер |
| `CommandSystemGroup` | **имя группы не менять** — дифф ради диффа |

Перенос файлов — `git mv`, чтобы сохранить .meta GUID.

Группа по-прежнему `OrderFirst` в `SimulationSystemGroup`. Явно зафиксировать порядок request-систем, которого не хватает:

```text
ProcessSimClock (буфер, OrderFirst)
ProcessSpawnAgent
ProcessPlaceBuilding
ProcessPlaceRoadStroke
ProcessDemolishRoad
ProcessDemolishBuilding          // UpdateAfter Place — закрыть гонку Ф1
ProcessSetPyramidFeed            // не менять семантику
ProcessSetActiveResearch
ApplySimDeltaTime OrderLast
```

Finalize больше нет.

---

## 2. SimCommands

Убрать fallback в `TryPost<T>`: если на сессии нет буфера `T` — `return false`, не искать «любую сущность с этим буфером».

`Request<T>`: уже вешает `RunScoped` с Ф1. Здесь только проверить, что дорожные хелперы тоже тегают.

Не вводить второй API. Presentation как звала `Request`/`TryPost`, так и зовёт.

---

## 3. Гейт «можно ли сейчас»

Один: `SimControl.SessionInGame != 0` (или читать `Mode != Off`, но SessionInGame ставит `GameEntry.Arm` — это «ран живой для игрока»).

Не проверять мёртвую фазу. Не ветвить `Source`.

Спавн из чита/кнопки SpawnWidget — request, проходит тот же гейт. Сценарий/сейв в Ф1 уже не ходят через request.

---

## 4. Чего не делать в Ф4

- Не переводить `PyramidFeedLine.PerHour` на запись из UI. Документ 07 §6.3 это предлагал — **отменено** до нового дизайна.
- Не переводить `DesiredWorkers` на request — это поле желания, уже правильно.
- Не трогать Stories-буферы (`AdvanceStorySystem`).
- Не «оптимизировать» `ToComponentDataArray` в request-системах: запросов единицы, копия нужна из-за структурных изменений. Документ 01 рефакторинга здесь ошибается.

---

## 5. Порядок работ

1. Таблица всех входов (grep `SimCommands.` и `SetComponentData` из Presentation). Классифицировать по таблице Ф0.5. Исключений быть не должно, кроме пирамиды-request (явный долг).
2. `git mv` файлов в `RequestSystems/`, переименовать типы систем. Починить `UpdateBefore/After` на новые имена.
3. Упростить гейт SessionInGame.
4. Убрать fallback `TryPost`.
5. Явный порядок demolish after place.
6. Grep `Consume*CommandsSystem` по Simulation — пусто (кроме часов, если оставите старое имя clock-системы; лучше `ConsumeSimClockCommandsSystem` оставить — это буфер, не request).

---

## 6. Grep-ворота

```
ConsumePlaceBuilding
ConsumeSpawnAgent
ConsumeDeconstruct
ConsumePlaceRoad
ConsumeDemolishRoad
ConsumeSetActiveResearch
```

`SimCommands.TryPost`: нет `CreateEntityQuery` внутри.

Каждый gameplay-вход из HUD классифицирован: поле / request / буфер.

---

## 7. Вики после Ф4

- [[../../Architecture/01 Folder Structure|01]] — `Requests/RequestSystems`
- [[../../Architecture/14 Sim Presentation Bridge|14]] — таблица входов
- `.cursor/rules/ecs-production-architecture.mdc` — папки фичи
- [[../07 Foundation Fixes|07]] §6; явно: слайдеры не сделаны
