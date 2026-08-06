# 00 Overview

← [[Index]] | [[../Home|Home]]

## Стек (целевой)

| Слой | Выбор |
| --- | --- |
| Engine | Unity 6, URP |
| Simulation | **DOTS / Entities** — source of truth рана |
| Shell / App | **AppFlow FSM** + Director + **SimGate**; сцены Root → Game |
| DI | **VContainer** только Shell/Presentation (паттерн gmtk Root/Game scopes) |
| Presentation | UI, камера, FMOD; читает ECS / шлёт commands |
| Content | Authoring + Baker, blobs, prefabs, таблицы баланса |
| Logging | `GameLog` + каналы (`Infrastructure/Logging`) |

Подробности: [[08 Production ECS]] · [[09 App Shell]].

## Слои

```
Shell (AppFlow, Director, SimGate, menus)
        ↓ SessionConfig + SimGate
Presentation (UI, камера, VFX, FMOD)
        ↓ Intent / Commands
Simulation ECS  ← истина рана
        ↑ Events / projections
Content (definitions, bake, баланс)
```

## Принципы

- Симуляция не знает про кнопки UI и меню
- UI шлёт команды; не считает производство/голод
- Shell включает симуляцию через SimGate (не «просто Play Mode»)
- Баланс и типы зданий — данными
- gmtk_2026 — референс **shell/DI сцен**, не card-core и не write model
- Обучение: [[07 Mentorship & Learning]]

---

Далее → [[01 Folder Structure]] · [[09 App Shell]]
