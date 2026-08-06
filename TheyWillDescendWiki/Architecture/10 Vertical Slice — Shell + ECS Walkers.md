# 10 Vertical Slice — Shell + ECS Walkers

← [[09 App Shell]] | [[Index]]

Целевой процесс **ближайших заходов**. Временное помечается явно.  
Камеры/состав сцен: [[11 Camera & Presentation Scenes]].

## Конечный UX

```text
Bootstrap (Root, всегда) — Main Camera, EventSystem, Startup
  → load MainMenu
       Press Any Key → Main Menu UI
       ↓ «Начать игру»
  LoadingGame — unload MainMenu, load Game, SimGate.Off
       ↓
  Playing — SimGate.Running
       ⇄ Paused (Esc, Frozen)
```

## Сцены (канон)

| Сцена | Что на ней |
| --- | --- |
| `Bootstrap` | Startup, одна Main Camera (+ AudioListener), EventSystem |
| `MainMenu` | Canvas splash/menu, ShellUiBinder |
| `Game` | мир Sample, свет, SubScene Simulation; Main Camera — временно до Cinemachine |

## Заходы

| # | Цель | Статус |
| --- | --- | --- |
| **A** | UI flow на одном Boot | done (эволюционирует в B) |
| **B** | Три сцены + load по правилам выше | **next** |
| **C** | ECS-ходьба | planned |
| **D** | Frozen стопает ходьбу | с C |

## Заход B — руками

1. `SampleScene` → Save As → `Assets/_Project/Scenes/Game.unity` (мир Sample).  
2. На Game: SubScene Simulation с Bootstrap; Startup/Canvas **не** копировать.  
3. New Scene → Save As `Assets/_Project/Scenes/MainMenu.unity`; перенеси туда Canvas + ShellUiBinder.  
4. Bootstrap: только Startup + Main Camera + EventSystem.  
5. Build Settings: Bootstrap (0), MainMenu, Game.  
6. Play с Bootstrap.

**Временно:** камера на Game до Cinemachine; челики MB до C.

---

Связанные: [[11 Camera & Presentation Scenes]] · [[09 App Shell]] · [[02 Scenes & Lifetime]]
