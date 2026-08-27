# 04 Simulation

← [[03 Core Systems]] | [[Index]] | Далее → [[05 Content Pipeline]]

## Домены

| Домен | В коде сейчас | Позже (GDD) |
| --- | --- | --- |
| City / Buildings | сетка, occupy, footprint, стройка, HQ | апгрейды, ротация |
| Agents / Workforce | спавн, plaza idle, commute, assign/unassign | pathfinding, нужды |
| Economy | ledger `ResourceAmount`, один выход на тип здания | рецепты, склады, логистика |
| Survival pressure | — | тепло/жизнь, голод |
| Gods | — | дань, гнев, фазы |
| Laws | — | модификаторы |
| WinLose | — | условия конца рана |

## Правило тика

Симуляция **работает**, только если `SimControl.IsRunning`. Меню и брифинг — `SessionInGame = 0` → Mode Off.

Внутри ECS:

```
CommandSystemGroup (consume + ApplySimDeltaTime последним)
  → commute / plaza / locomotion
  → construction
  → produce
  → day clock
```

Presentation отображает и шлёт Intent/Commands вниз. Оболочка: [[09 App Shell]].

## Временное (явно)

- Один слот рабочего на здание
- Один выход (`ProduceResourceId` + rate) на `BuildingType` — не рецепт
- Имя `SimBridge` на session — флаги despawn + `NextAgentId`, не «мост UI»

Производство идёт, только если `Workplace.Working` (рабочий **дошёл**), не просто assigned.

---

Связанные разделы: [[../GDD/03 City & People|City & People]] · [[../GDD/04 Economy & Heat|Economy & Heat]] · [[08 Production ECS]]
