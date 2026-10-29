# 11 Camera & Presentation Scenes

← [[02 Scenes & Lifetime]] | [[Index]]

Канон презентации: **у каждой контентной сцены своя Main Camera и свой слушатель FMOD**. Сцены сменяют друг друга Single-загрузкой, поэтому одновременно живёт ровно одна камера и один `StudioListener`.

## Зачем так

Раньше была одна камера на вечной Root-сцене. Из-за этого Game не стартовала без Root: рендерить её VCam и слышать мир было нечем. Камера — часть сцены, как свет: сцена, которая сама себе точка входа, должна уметь себя показать.

Две камеры одновременно (конфликт `StudioListener`, clear flags, stack) исключены не правилом «камера одна на процесс», а правилом «сцены не грузятся друг на друга».

## Что где лежит

### AppScope — процесс

| Объект | Да / нет | Почему |
| --- | --- | --- |
| `EventSystem` | **да** | один на процесс; в сценах запрещён |
| `LoadingScreen` (overlay-canvas) | **да** | переживает смену сцен |
| Shell-сервисы, `InputService`, `AudioService` | **да** | обычные классы в контейнере, не объекты |
| Камера / слушатель / свет | **нет** | у сцен |

### MainMenu

| Объект | Да / нет |
| --- | --- |
| Main Camera + `StudioListener` | **да**. Без `CinemachineBrain`, пока у меню нет VCam |
| Canvas (Press Any Key, Main Menu) | **да** |
| `PressAnyKeyScreen` / `MainMenuScreen` / `MainMenuFlow` | **да** |
| VCam меню | позже, при 3D-фоне (тогда добавить Brain) |
| `EventSystem` | **нет** |

### Game и сцены рана

Сцена рана = общий префаб `#GameRun` + свои декорации `@Dressing`. Таких сцен две: `Game.unity` и `Blockout/GameBlockout.unity`. Подробно: [[../Refactoring/09 Dressing & Blockout|09 Декорации и блокаут]].

```text
#GameRun        Prefabs/Game/GameRun.prefab — функционал, ни одной ссылки на арт
   #Scope       GameLifetimeScope, GameSession
   #Cameras     Main Camera (Brain + StudioListener), VCam_Gameplay, PauseCamera, RTSCameraTarget
   #City        RadialGridGuide, BuildPlacement, Headquarters (PyramidView, клик, площадь)
   #Agents      AgentSpawner, Humans
   #WorldAudio  AudioZoneManager, ZoneAmbienceSfxScheduler, WindAmbience
   #Hud         GameHudCanvas
@Dressing       SceneDressing → VisualSet этой сцены
   @Lighting    свет, volume, смена дня
   @Environment земля, вода, горы
   @Pyramid     модель пирамиды
$Dev            ScenarioEditor (только GameBlockout)
```

Префиксы `#` `@` `$` — палитра `Colourful Hierarchy` (красит строку в Hierarchy, в игре ничего не стоит).

| Объект | Где |
| --- | --- |
| Камеры, HUD, `WorldAudio`, сессия, scope | `#GameRun`, одинаково во всех сценах рана |
| Свет, volume, окружение, модель пирамиды | `@Dressing`, своё в каждой сцене |
| Модели зданий и людей | `VisualSet` с `@Dressing` → `BuildingViewBoard`, `BuildPlacementController`, `AgentViewBoard` |
| `$Dev` / `ScenarioEditor` | только `GameBlockout` — мастерская стартового сценария |
| `EventSystem` | **нет** |

Правило: **Game не ссылается на блокаут.** Блокаут берёт `#GameRun` и меняет только своё. Правка префаба `#GameRun` — правка обеих сцен.

## Cinemachine

Пакет: `com.unity.cinemachine` **3.x**.

| Где | Компонент |
| --- | --- |
| Game Main Camera | `CinemachineBrain` + `StudioListener` |
| Game | `VCam_Gameplay` (`CinemachineCamera`) + `RTSCameraTarget` |
| MainMenu | позже VCam меню при 3D-фоне |

### RTS-риг на Game

Brain смотрит на виртуальную камеру. Сама VCam не рендерит — она задаёт позу и линзу.

| Объект | Роль |
| --- | --- |
| `RTSCameraTarget` | точка орбиты; пан WASD двигает её по земле |
| `VCam_Gameplay` | `CinemachineCamera` + `CinemachineOrbitalFollow` + `CinemachineHardLookAt` + `CinemachineInputAxisController` + `RTSCameraController` |

Управление:

| Ввод | Что делает |
| --- | --- |
| WASD | пан цели по XZ относительно взгляда |
| Shift | спринт пана |
| колёсико | зум по шагам: радиус 5–15, 4 шага; угол по кривой `angleCurve` |
| ПКМ / СКМ + мышь | орбита по yaw вокруг цели |

Лимит пана: круг `maxMapRadius = 55` вокруг `(0,0,0)`. Pitch от зума, не от мыши — иначе контроллер и ось Y орбиты дерутся каждый кадр. Самый дальний шаг зума глушит локальные звуковые ячейки (`RTSCameraController.IsFullyZoomedOut`).

`RTSCameraController` — `Presentation/Camera`, namespace `TheyWillDescend.Presentation.Cameras` (во множественном числе, чтобы не спорить с `UnityEngine.Camera`). Внедряется через `GameLifetimeScope`.

## Play Mode vs Editor

| Режим | Что открыть | Что происходит |
| --- | --- | --- |
| **Play из меню** | `MainMenu.unity` | сплэш → меню → по Start экран загрузки и Game |
| **Play из игры** | `Game.unity` | ран по `editorSaveId` / `editorDebug` на `GameLifetimeScope` |
| **Play блокаута** | `Blockout/GameBlockout.unity` | тот же ран, пока сцена открыта в редакторе. Рестарт и старт из меню открывают `Game` |
| **Edit уровня / UI** | нужную сцену | открывать по одной: две контентные сцены в Play стартуют обе |

`LoadSceneAsync` по имени **требует**, чтобы сцена была в Build Profiles / Build Settings. Build list: `MainMenu` (0), `Game`. Шелл грузит только эти две. Блокаут в сборке нет, и переход на него кода не имеет.

## Итог одной фразой

**AppScope = процесс (ввод, звук, переходы, EventSystem). Каждая сцена = свой глаз и уши. MainMenu = экраны. Сцена рана = общий `#GameRun` + свои декорации и облик.**

---

Связанные: [[02 Scenes & Lifetime]] · [[09 App Shell]] · [[../GDD/08 UI & Visual|GDD UI]]
