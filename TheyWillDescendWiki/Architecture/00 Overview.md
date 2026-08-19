# 00 Overview

← [[Index]] | [[../Home|Home]]

## Стек (целевой)

| Слой | Выбор |
| --- | --- |
| Engine | Unity 6, URP |
| Simulation | **DOTS / Entities** — source of truth рана |
| Shell / App | **AppFlow FSM** + **SimGate** (код в Presentation; вход — Main); сцены Root → Game |
| DI | **VContainer** позже, только Presentation (не внутри `ISystem`) |
| Presentation | UI, камера, FMOD, Shell; читает ECS / шлёт commands |
| Content | Authoring + Baker, blobs, prefabs, таблицы баланса |
| Logging | `GameLog` (`Presentation/Infrastructure/Logging`) |
| Сборки | четыре стены — [[01 Folder Structure]] |

Подробности: [[08 Production ECS]] · [[09 App Shell]].

## Слои

```
Main (Startup, регистрация)
        ↓
Presentation (Shell FSM, SimGate, UI, камера, FMOD)
        ↓ Intent / Commands
Simulation ECS  ← истина рана
        ↑ pull / reject-события
Content (Authoring, bake, баланс)
```

Сборки (стены компилятора) — [[01 Folder Structure]]. Shell — роль и папка, не asmdef.

## Принципы

- Симуляция не знает про кнопки UI и меню
- UI шлёт команды; не считает производство/голод
- Shell включает симуляцию через SimGate (не «просто Play Mode»)
- Баланс и типы зданий — данными
- gmtk_2026 — референс **shell/DI сцен**, не card-core и не write model
- Обучение: [[07 Mentorship & Learning]]

---

Далее → [[01 Folder Structure]] · [[09 App Shell]]
