# 01 Folder Structure

← [[00 Overview]] | [[Index]] | Далее → [[02 Scenes & Lifetime]]

## Закон

**Сборка = стена компилятора** (кто не видит кого, что не попадёт в билд).  
**Папка внутри = домен.** Не зеркало Clean Architecture и не туториальный `Components/` vs `Systems/`.

Новая asmdef оправдана, только если без неё либо запретная ссылка компилируется, либо в билд едет код, которого там быть не должно.

```
Authoring  →  Simulation
Presentation  →  Simulation
Main  →  Presentation
```

`TheyWillDescend.Simulation` **не** ссылается на Presentation, uGUI, Input System, Animator.  
`TheyWillDescend.Authoring` **не** ссылается на Presentation.  
Simulation и Authoring: `autoReferenced: false` — на них ссылаются явно.

## Сборки

| Asmdef | Что внутри | Нельзя |
| --- | --- | --- |
| `TheyWillDescend.Simulation` | компоненты, системы, `SimIo`, сетка, occupancy | UI, виды, `SimGate`, диск |
| `TheyWillDescend.Authoring` | Baker’ы SubScene | runtime UI |
| `TheyWillDescend.Presentation` | HUD, ghost, view boards, Shell FSM, `SimGate`, JSON-сейв, `GameLog` | `ISystem`; писать стоки/occupy в обход команд |
| `TheyWillDescend.Main` | `Startup`, `AppFlowFactory` — вход и регистрация | экономика, `EntityManager` |

Домены Shell / Application / Infrastructure — **папки** в Presentation, не отдельные сборки.  
`TheyWillDescend.App` как имя сборки не используем: внутри `TheyWillDescend.*` оно затеняет `UnityEngine.Application`.

Позже, когда заболит: Editor-tooling, dedicated server, чужие SDK (FMOD/Steam) — тогда новая стена.

## Папки

```
Assets/_Project/Scripts/
  Simulation/       Time Agents City Session Io
  Authoring/        Time Session
  Presentation/     Agents City GameHud ShellUi
                    Shell/            FSM, SimGate, сцены
                    Application/      Capture/Apply слота
                    Infrastructure/   Logging Save
  Main/             Startup, AppFlowFactory
```

Новая механика: система в `Simulation/<домен>`, команда в `Io` (или рядом с доменом), вид в `Presentation/<домен>`. HUD только `SimIo.TryEnqueue…`.

Связанные: [[00 Overview]] · [[08 Production ECS]] · [[09 App Shell]] · [[14 Sim Presentation Bridge]]
