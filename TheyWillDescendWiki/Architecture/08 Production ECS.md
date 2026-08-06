# 08 Production ECS

← [[07 Mentorship & Learning]] | [[Index]] | [[../Home|Home]]

Целевая **production**-архитектура симуляции: зрелая, расширяемая, под масштаб Frostpunk-like проекта. Не «демо Entities», а каркас большой игры.

## Закон

> **Write model симуляции — ECS. Всё остальное — адаптеры.**

UI / FMOD / камера не считают экономику. Authoring и ScriptableObjects — design-time контент; в Play истина — entities (и readonly blobs).

## Слои

```
Presentation     UI, camera, selection, FMOD, VFX
        ↓ Intent (assign, build, law…)
Application      тонкий: Intent → Command (без economy math)
        ↓ Commands
Simulation       ECS world — source of truth
        ↑ Events / projections
Content          Authoring, Baker, blobs, prefabs, balance
```

## Домены Simulation

Папки + SystemGroups + соглашения по компонентам:

| Домен | Ответственность |
| --- | --- |
| Time | день/тик, пауза, фазы суток |
| Commands | входящие намерения игрока/AI |
| City | здания, стройка, слоты |
| Workforce | рабочие, assignment, idle |
| Economy | склады, рецепты, логистика |
| Survival | голод, cold/heat / давление |
| Society | hope/discontent, законы как модификаторы |
| Gods | дань, фазы, гнев |
| Crisis | кризисы, таймеры давления |
| Events | факты наружу (для UI/аудио) |
| WinLose | условия конца рана |

### Порядок тика (pipeline)

```
ReceiveCommands
  → AdvanceTime
  → Assignment
  → Production
  → Consumption / Needs
  → Morale / Laws
  → Gods / Crisis
  → WinLose
  → EmitEvents
```

## DI

| Слой | DI |
| --- | --- |
| Simulation | **Нет** контейнера. Singletons, queries, ECB, SystemGroups |
| Presentation / Infra | Опционально тонкий (UI, FMOD, save) |

Антипаттерн: god-сервис `IEconomyService`, который дергают и UI, и systems.

Паттерн:

```
UI → AssignWorkerCommand
AssignmentSystem → Workforce / Workplace
ProductionSystem → Stock
UI ← читает Stock / domain events
```

## Точки расширения

| Добавляем | Куда | Не переписываем |
| --- | --- | --- |
| Ресурс | content + stock | весь UI |
| Здание | authoring prefab + recipe data | ядро production «на каждый тип» |
| Закон | modifier components / tags | разрозненные if по коду |
| Кризис | event entity + Crisis group | экономику с нуля |
| Другой UI | Presentation | Simulation |

## Ранние решения

- Геймплей: **Frostpunk assign/build**, не card DnD ([[../GDD/00 Overview|GDD]])
- Workforce v1: **агрегаты** (`AssignedCount`), агенты с pathfinding позже
- Время: **singleton** `GameTime` (уже в коде)
- Сцены: Root + additive Game + SubScene Simulation — см. [[09 App Shell]]
- SimGate: дни/экономика только в **Running**
- Логи: `GameLog` + каналы

## Анти-паттерны

- Production в `Button.onClick`
- Rich OO aggregates с логикой на entity
- SO как runtime write model
- Burst/ISystem → прямые вызовы UI/FMOD
- Копирование card-архитектуры джема в core
- Симуляция тикает на меню/брифинге без SimGate
- VContainer-сервис как write model экономики

## Карта с бэкенд-DDD

| Сохранить | Заменить |
| --- | --- |
| Границы доменов, язык | Методы на aggregate |
| Инварианты в правилах | «Сервис на всё» |
| Domain events | Скрытый bus без контракта |
| Content vs runtime | SO = истина в Play |
| Application workflow | AppFlow FSM в Shell ([[09 App Shell]]) |

---

Связанные: [[04 Simulation]] · [[07 Mentorship & Learning]] · [[00 Overview]] · [[02 Scenes & Lifetime]] · [[09 App Shell]]