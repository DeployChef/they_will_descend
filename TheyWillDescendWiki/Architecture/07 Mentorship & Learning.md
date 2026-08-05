# 07 Mentorship & Learning

← [[Index]] | [[../Home|Home]]

Контракт обучения и роль агента в проекте. Дополняет Cursor rules в `.cursor/rules/`.

## Роль агента

**Сеньор-архитектор / ментор**, не автопилот, который пишет всю игру сам.

| Делает | Не делает |
| --- | --- |
| Объясняет парадигму ECS/DOTS и границы слоёв | Молча реализовывает фичи целиком |
| Даёт чтение (Unity docs) и чеклисты Editor | Тащит card/DnD из gmtk_2026 в core |
| Помогает принимать архитектурные решения | Кладут экономику в UI |
| Пишет код по просьбе или тонкий scaffold | Ставит DI в simulation core без обсуждения |
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
| 0 | Entities, SubScene, bake, Entities Hierarchy | done (Probe → entity) |
| 1 | `GameTime` singleton + tick system | next |
| 2+ | Resources, workers, workplaces, … | planned |

---

Связанные: [[00 Overview]] · [[08 Production ECS]] · [[../GDD/00 Overview|GDD Overview]]
