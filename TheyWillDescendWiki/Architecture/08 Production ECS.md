# 08 Production ECS

← [[07 Mentorship & Learning]] | [[Index]] | [[../Home|Home]]

Целевая **production**-архитектура симуляции: зрелая, расширяемая, под масштаб Frostpunk-like AA-проекта. Не «демо Entities».

## Закон

> **Write model симуляции — ECS. Всё остальное — адаптеры.**

UI / FMOD / камера не считают экономику. Authoring и ScriptableObjects — design-time контент; в Play истина — entities.

## Слои

```
Main             Startup / регистрация FSM
Presentation     UI, camera, Shell, FMOD; Intent → Command (без economy math)
        ↓ SimCommands.TryPost
Simulation       ECS world — source of truth
        ↑ pull / редкие reject-события
Content          Authoring, Baker, prefabs, balance
```

Сборки — [[01 Folder Structure]]. «Application» как тонкий use-case (сейв-снимок) живёт **папкой** в Presentation, не отдельной asmdef.

## Домены Simulation

Папки + SystemGroups:

| Домен | Ответственность | Сейчас |
| --- | --- | --- |
| Time | сутки (`GameTime`) | да |
| Session | `SimControl`, команды, despawn-флаги | да |
| City | здания, стройка, сетка, occupy | да |
| Agents | рабочие, assignment, commute | да (без pathfinding) |
| Economy | склады, производство | да (рецепт на типе) |
| Survival | голод, cold/heat | нет |
| Society | hope/discontent, законы | нет |
| Gods | дань, эры, лояльность, печь HQ | срез 1 |
| Crisis | кризисы | нет |
| WinLose | конец рана | нет |

### Порядок тика (pipeline)

```
CommandSystemGroup (OrderFirst в SimulationSystemGroup)
  consume clock / spawn / place / assign / scenario / despawn
  ApplySimDeltaTime (OrderLast в группе)
  → commute / plaza idle / locomotion
  → construction
  → production (только Workplace.Working)
  → pyramid burn 24/7 (не Workplace; после домов)
  → AdvanceGameTime
  → era boundary (день+час) / loyalty lerp
```

Needs / Laws / Gods / Crisis / WinLose — группы появятся, когда появится домен.

## DI

| Слой | DI |
| --- | --- |
| Simulation | **Нет** контейнера. Singletons, queries, ECB, SystemGroups |
| Presentation | Опционально тонкий (UI, FMOD, save) |

Антипаттерн: god-сервис `IEconomyService`, который дергают и UI, и systems.

Паттерн:

```
UI → AssignWorkerCommand
ConsumeAssignWorker → AgentAssignment / Workplace
AdvanceAgentCommute → Workplace.Working
ProduceResourceSystem → ResourceAmount
UI ← читает ledger / Construction
```

## Точки расширения

| Добавляем | Куда | Не переписываем |
| --- | --- | --- |
| Ресурс | content + `ResourceAmount` | весь UI (чипы HUD пока сценой — временно) |
| Здание | `BuildingDefinition` + prefab + catalog | ядро production «на каждый тип» |
| Закон | modifier components / tags | разрозненные if по коду |
| Кризис | event entity + Crisis group | экономику с нуля |
| Другой UI | Presentation | Simulation |

## Ранние решения (обновлено)

- Геймплей: **Frostpunk assign/build**, не card DnD ([[../GDD/00 Overview|GDD]])
- Workforce: **агенты** с assignment и commute. Pathfinding — позже. Один слот на здание — временно
- Время: `GameTime` + `SimControl` (не отдельный `SimClock`, не `SimGate`)
- Сцены: Bootstrap + additive MainMenu / Loading / Game + SubScene — [[09 App Shell]]
- Тик экономики только при `SimControl.IsRunning`
- Логи: `GameLog` + каналы

## Анти-паттерны

- Production в `Button.onClick`
- Rich OO aggregates с логикой на entity
- SO как runtime write model
- Burst/ISystem → прямые вызовы UI/FMOD
- Presentation `SystemBase`, который держит `TimeWidget` / `*ViewBoard` (вид **pull**, не push)
- Копирование card-архитектуры джема в core
- Симуляция тикает на меню/брифинге (`SessionInGame = 0` должно быть Off)
- VContainer-сервис как write model экономики
- `Time.timeScale` как пауза города

## Карта с бэкенд-DDD

| Сохранить | Заменить |
| --- | --- |
| Границы доменов, язык | Методы на aggregate |
| Инварианты в правилах | «Сервис на всё» |
| Domain events | Скрытый bus без контракта |
| Content vs runtime | SO = истина в Play |
| Application workflow | AppFlow FSM в Shell ([[09 App Shell]]) |

---

Связанные: [[04 Simulation]] · [[07 Mentorship & Learning]] · [[00 Overview]] · [[02 Scenes & Lifetime]] · [[09 App Shell]] · [[14 Sim Presentation Bridge]]
