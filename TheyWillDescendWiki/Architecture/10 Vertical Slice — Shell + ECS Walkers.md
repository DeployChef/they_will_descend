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
| `Game` | мир, свет, SubScene Simulation, `VCam_Gameplay` + `RTSCameraTarget` |

## Заходы

| # | Цель | Статус |
| --- | --- | --- |
| **A** | UI flow на одном Boot | done (эволюционирует в B) |
| **B** | Три сцены + load по правилам | done |
| **C+D** | ECS-ходьба + Frozen стопает | **done** (entity + view board) |

## SubScene vs Game vs спавн (важно)

| Что | Где | Почему |
| --- | --- | --- |
| `GameTime`, `SimControl`, `ResourceAmount` | **Simulation SubScene** | bake → session entity, данные рана |
| Плаза HQ | **Simulation SubScene** | bake → `Building` + `Headquarters` + EG-меш; `CityGrid.Center` с его `LocalTransform` |
| Статичный декор (деревья, скалы) | **Game** сцена | обычные GO, не симуляция |
| Челики (skinned + Animator) | **Game** + runtime entity | entity в ECS; вид (`AgentViewBoard`) снаружи |
| Новые челики / дома игрока | команды `SpawnAgent` / `PlaceBuilding` | динамика = `Instantiate`, не bake |

SubScene **нужна** для baked sim-данных (время, контроль, позже здания/рецепты).  
Она **не обязана** содержать всех агентов. Динамическое население = команда `SpawnAgent` → entity; `AgentViewBoard` ставит меш.

HUD: `GameHudCanvas` на Game (overlay). Часы — `TimeWidget` на `TimeBar`; сток — `ResourceWidget` сверху слева; инспектор дома — правая плашка (`BuildingInspectPanel`).


## Заход B — руками

1. `SampleScene` → Save As → `Assets/_Project/Scenes/Game.unity` (мир Sample).  
2. На Game: SubScene Simulation с Bootstrap; Startup/Canvas **не** копировать.  
3. New Scene → Save As `Assets/_Project/Scenes/MainMenu.unity`; перенеси туда Canvas + ShellUiBinder.  
4. Bootstrap: только Startup + Main Camera + EventSystem.  
5. Build Settings: Bootstrap (0), MainMenu, Game.  
6. Play с Bootstrap.

**Временно:** челики-вид — MB board, не C.

---

Связанные: [[11 Camera & Presentation Scenes]] · [[09 App Shell]] · [[02 Scenes & Lifetime]]
