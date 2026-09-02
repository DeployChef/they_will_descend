# 04 Simulation

← [[03 Core Systems]] | [[Index]] | Далее → [[05 Content Pipeline]]

## Домены

| Домен | В коде сейчас | Позже (GDD) |
| --- | --- | --- |
| City / Buildings | сетка, occupy, footprint, стройка, HQ | апгрейды, ротация |
| Agents / Workforce | спавн, plaza idle, commute, assign/unassign | pathfinding, нужды |
| Economy | ledger `ResourceAmount`, рецепт на типе здания | cap стока от зданий, склады, энергия |
| Survival pressure | — | тепло/жизнь, голод |
| Gods | эры, лояльность, печь HQ | обряд (люди/кристаллы), катастрофы |
| Laws | — | модификаторы |
| WinLose | — | условия конца рана |

## Правило тика

Симуляция **работает**, только если `SimControl.IsRunning`. Меню и брифинг — `SessionInGame = 0` → Mode Off.

Внутри ECS:

```
CommandSystemGroup
  → clock → reset agents → reset buildings
  → scenario spawn → spawn → place
  → assign → unassign → workplace pause → pyramid feed
  → FinalizeSimSessionLifecycle
  → ApplySimDeltaTime
  → commute / plaza / locomotion
  → construction
  → produce
  → day clock
```

Presentation отображает и шлёт Intent/Commands вниз. Оболочка: [[09 App Shell]].

## Временное (явно)

- Один слот save JSON

Session-root — `SimSession` с unmanaged-фазой `Unprepared → Preparing → Ready` и `Ready → Resetting → Unprepared`; `AgentIdSequence` и типизированные lifecycle command buffers хранят отдельные ответственности. Finalizer подтверждает переход только после drain всех входящих setup/reset-команд. Shell ждёт эту фазу асинхронно и никогда не запускает consume вручную.

Производство: `WorkingCount / WorkplaceSlots` × рецепт (дошедшие). Бар над домом: `AssignedCount / слоты` (кого назначил).

---

Связанные разделы: [[../GDD/03 City & People|City & People]] · [[../GDD/04 Economy & Heat|Economy & Heat]] · [[08 Production ECS]]
