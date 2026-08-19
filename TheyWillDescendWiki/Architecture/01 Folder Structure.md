# 01 Folder Structure

← [[00 Overview]] | [[Index]] | Далее → [[02 Scenes & Lifetime]]

## Закон

Сборка = слой. Папка внутри = домен. Не туториальный `Components/` vs `Systems/`.

Граф только вниз. Чужой `using` не компилируется.

```
Editor (позже)
  → Authoring → Presentation → Simulation
         Main ↗              ↗
         Shell → Simulation, Infrastructure
         Application → Simulation, Shell, Infrastructure
         Infrastructure (лог, DTO/файл)
```

`TheyWillDescend.Simulation` **не** ссылается на Presentation, Shell, uGUI, Input System. Transform/Animator туда не импортируются.

## Сборки

| Asmdef | Что внутри | Нельзя |
| --- | --- | --- |
| `TheyWillDescend.Simulation` | компоненты, системы, `SimIo`, сетка, occupancy | UI, виды, `SimGate` |
| `TheyWillDescend.Authoring` | Baker’ы SubScene | runtime UI |
| `TheyWillDescend.Presentation` | HUD, ghost, view boards | писать стоки/occupy |
| `TheyWillDescend.Shell` | FSM, `SimGate`, сцены | `ISystem`, виды |
| `TheyWillDescend.App` | Capture/Apply слота | GameObject Instantiate. Не `Application`: внутри `TheyWillDescend.*` это затеняет `UnityEngine.Application` |
| `TheyWillDescend.Infrastructure` | `GameLog`, JSON DTO | ECS, виды |
| `TheyWillDescend.Main` | `Startup`, `AppFlowFactory` | единственный, кто видит Shell **и** Presentation |

## Папки

```
Assets/_Project/Scripts/
  Simulation/     Time Agents City Session Io
  Authoring/      Time Session
  Presentation/   Agents City GameHud ShellUi
  Shell/          States
  Application/
  Infrastructure/ Logging Save
  Main/
```

Новая механика: система в `Simulation/<домен>`, команда+событие в `Io` (или рядом с доменом), вид в `Presentation/<домен>`. HUD только `SimIo.TryEnqueue…`.

Связанные: [[00 Overview]] · [[08 Production ECS]] · [[09 App Shell]] · [[14 Sim Presentation Bridge]]
