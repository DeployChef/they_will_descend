# 00 Overview

← [[Index]] | [[../Home|Home]]

## Стек (целевой)

| Слой | Выбор |
| --- | --- |
| Engine | Unity 6, URP |
| Simulation | **DOTS / Entities** (production ECS) — source of truth |
| Presentation | GameObjects / UI Toolkit / uGUI + читает ECS |
| Audio | FMOD (снаружи симуляции) |
| Content | Authoring + Baker, blobs, prefabs, таблицы баланса |
| DI | **Не в core симуляции.** Опционально только Presentation/Infra |

Подробности: [[08 Production ECS]].

## Слои

```
Presentation (UI, камера, VFX, FMOD)
        ↓ Intent
Application (тонкий: Intent → Command)
        ↓
Simulation ECS (город, люди, экономика, боги)  ← истина
        ↑ Events / projections
Content (definitions, bake, баланс)
```

## Принципы

- Симуляция не знает про конкретные кнопки UI
- UI шлёт команды; не считает производство/голод
- Баланс и типы зданий — данными, не ветками `if` на каждый тип
- gmtk_2026 — референс сеттинга, не card-архитектуры
- Обучение и роль агента: [[07 Mentorship & Learning]]

---

Далее → [[01 Folder Structure]] · [[08 Production ECS]]
