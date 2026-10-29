# 00 Overview

← [[Index]] | [[../Home|Home]]

## Стек (как в коде)

| Слой | Выбор |
| --- | --- |
| Engine | Unity 6, URP |
| Simulation | **DOTS / Entities** — единственный write model рана |
| Shell / App | `AppScope` — процесс; `AppContext` помнит запуск и сплэш; `ShellService` грузит сцены; `GameEntry` в сцене Game стартует ран и включает часы. Любая сцена — точка входа. Верхней FSM нет |
| Часы рана | `SimControl` + `SimClockCommand` в ECS. Нет `SimGate`, нет `timeScale` |
| DI | **VContainer** в Main/Presentation (не внутри `ISystem`) |
| Presentation | UI, камера, FMOD, Shell; читает ECS / шлёт commands |
| Content | Authoring + Baker, ScriptableObjects, префабы |
| Logging | `GameLog` (`Presentation/Infrastructure/Logging`) |
| Сборки | стены компилятора — [[01 Folder Structure]] |

Подробности: [[08 Production ECS]] · [[09 App Shell]].

## Слои

```
Main (AppScope + scope'ы сцен — только композиция)
        ↓
Presentation (Shell-сервисы, HUD, камера, AudioService, мост SimBridge)
        ↓ поле желания  или  разовый запрос
Simulation ECS  ← истина рана
        ↑ pull / BuildingRejectedEvent
Content (Authoring, bake, баланс)
```

Сборки — стены компилятора. Shell — роль и папка в Presentation, не отдельная asmdef.

## Принципы

- Симуляция не знает кнопки, меню, Animator, FMOD
- UI пишет желание на сущность или шлёт разовый запрос; не пишет склад и не считает производство
- Ран тикает, только если `SimControl.Mode == Running` (session in-game, не player-pause, не build-lock)
- Баланс и типы зданий — данными
- gmtk_2026 — сеттинг, не card-core и не write model
- Обучение: [[07 Mentorship & Learning]]

---

Далее → [[01 Folder Structure]] · [[09 App Shell]]
