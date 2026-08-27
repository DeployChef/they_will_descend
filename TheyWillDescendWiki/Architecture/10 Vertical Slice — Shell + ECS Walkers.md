# 10 Vertical Slice — Shell + ECS Walkers

← [[09 App Shell]] | [[Index]]

Срез, который **уже в коде**. Временное помечается явно.  
Камеры/состав сцен: [[11 Camera & Presentation Scenes]].

## Конечный UX

```text
Bootstrap (Root, всегда) — камера, EventSystem, Startup, GameAudio, GameInput, GameSession
  → load MainMenu
       Press Any Key → Main Menu UI
       ↓ «Начать игру»
  LoadingGame — Loading + Game, unload MainMenu, SimControl Off
       ↓
  Playing — SimClockCommand.InGame(true)
       ⇄ PlayerPaused (Esc / ⏸), FSM остаётся Playing
```

## Сцены (канон)

| Сцена | Что на ней |
| --- | --- |
| `Bootstrap` | хосты + одна Main Camera (+ AudioListener) + EventSystem |
| `MainMenu` | Canvas splash/menu, `PressAnyKeyScreen`, `MainMenuScreen` |
| `Loading` | переход в ран |
| `Game` | мир, свет, HUD, SubScene Simulation, `VCam_Gameplay` + `RTSCameraTarget` |

Build list: Bootstrap (0), MainMenu, Loading, Game.

## Что уже работает

| Кусок | Статус |
| --- | --- |
| Меню → Loading → Playing | done |
| ECS-ходьба + Mixamo view | done |
| Пауза / x1 x2 x3 + часы HUD | done |
| Сток wood/food | done |
| Стройка на полярной сетке + occupy ECS | done |
| Назначение рабочих (до 10 на дом; производство × нагрузка) | done |
| Сценарий bake (стартовый запас + дома + люди) | done |
| Save/load слот JSON | done |
| Нужды / кризис / win-lose | нет — следующий рост петли |

## SubScene vs Game vs спавн

| Что | Где | Почему |
| --- | --- | --- |
| `GameTime`, `SimControl`, `ResourceAmount`, каталоги | **Simulation SubScene** | bake → session entity |
| Плаза HQ | **Simulation SubScene** | bake → `Building` + `Headquarters`; `CityGrid.Center` с `LocalTransform` |
| Статичный декор | **Game** сцена | обычные GO |
| Челики (skinned + Animator) | **Game** + runtime entity | entity в ECS; вид (`AgentViewBoard`) снаружи |
| Новые челики / дома игрока | команды `SpawnAgent` / `PlaceBuilding` | динамика = Instantiate, не bake |

На GO `SimControl` в SubScene (соседи authoring):

1. `SimControlAuthoring` — часы + `SimBridge` + `SimClockCommand`
2. `AgentSessionAuthoring` — spawn/assign буферы + штамп агента
3. `CityGridAuthoring` — сетка + occupy + place/reject
4. `BuildingCatalogAuthoring` / `ResourceCatalogAuthoring`

Рядом, **не** на том же GO: `Scenario` + `ScenarioAuthoring`, HQ, `GameTimeAuthoring`.

HUD: `GameHudCanvas` на Game. Часы — `TimeWidget`; сток — `ResourceWidget`; инспектор — `BuildingInspectPanel` (ссылка на `BuildingSelection`).

Стройка: **ЛКМ** ставит и остаётся в режиме, пока после `Playback()` ещё хватает ресурса. **ПКМ** и **Esc** отменяют.

---

Связанные: [[11 Camera & Presentation Scenes]] · [[09 App Shell]] · [[02 Scenes & Lifetime]] · [[05 Content Pipeline]]
