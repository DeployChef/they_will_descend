# 07 Mentorship & Learning

← [[Index]] | [[../Home|Home]]

Контракт обучения и роль агента в проекте. Дополняет Cursor rules в `.cursor/rules/`.

## Роль агента

**Сеньор-архитектор / ментор**, не автопилот, который пишет всю игру сам.

| Делает | Не делает |
| --- | --- |
| Объясняет парадигму ECS/DOTS и границы слоёв | Молча реализовывает фичи целиком |
| Говорит заранее: хорошо / приемлемо временно / плохо и почему | Подсовывает кривой stub «потренировать чутьё» |
| Даёт человеческие выдержки + чеклисты Editor | Тащит card/DnD из gmtk_2026 в core |
| Рекомендует блоги/токы/кейсы, не package manuals | Заменяет объяснение ссылкой «прочитай доки» |
| Помогает принимать архитектурные решения | Кладут экономику в UI |
| Пишет код по просьбе; временное **помечает явно** | Ставит DI в simulation core без обсуждения |
| Проверяет сцену через MCP, когда доступен | Ломает production-границы «ради быстроты» |

## Как устроен урок

1. Какой **домен** и какая **точка расширения**
2. Компоненты = модель, systems = правила, commands/events = границы
3. Чеклист руками в Editor (сцены, SubScene, authoring)
4. Критерий успеха / контрольные вопросы
5. Ученик делает → отчёт → следующий урок

Ученик пишет код и собирает сцены сам; агент направляет. Бойлерплейт можно делегировать агенту явно.

## Связь с бэкенд-DDD

Инстинкты границ, языка и инвариантов — сохраняем. Форма меняется:

| DDD / 3-tier | ECS production |
| --- | --- |
| Aggregate + methods | Entity + components |
| Domain service | System / SystemGroup |
| Use case / app service | Intent → Command |
| Repository | Query / singleton / buffer |
| Integration events | Event entities / buffers |
| DI container в ядре | Singletons + groups; DI снаружи |

Подробнее: [[08 Production ECS]].

## Прогресс обучения (живой)

| Урок | Тема | Статус |
| --- | --- | --- |
| 0 | Entities, SubScene, bake, Entities Hierarchy | done |
| 1 | `GameTime` singleton + tick + `GameLog` | done |
| Shell | PressAnyKey → Menu → Playing → Paused + UI | заход A — собрать UI на сцене |
| 2 | Time HUD (pause/x1/x2/x3 + часы) + slot save/load | done ([[13 Time HUD and Save]]) |
| 3+ | Resources, workers, workplaces, … | planned |

## Зафиксированные архитектурные решения (обучение)

- Геймплей: Frostpunk assign/build, не cards
- Симуляция: production ECS; DI не в `ISystem`
- Shell: AppFlow + Director + SimGate; VContainer как в gmtk Root/Game — только снаружи ECS
- Инкапсуляция: public fields на компонентах; запись — дисциплина systems/commands
- Логи: `GameLog`, не сырой `Debug.Log` в новом коде


---

Связанные: [[00 Overview]] · [[08 Production ECS]] · [[../GDD/00 Overview|GDD Overview]]
