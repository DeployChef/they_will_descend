# 11 Camera & Presentation Scenes

← [[02 Scenes & Lifetime]] | [[Index]]

Канон презентации: **одна реальная камера на Root**, остальное — виртуальные камеры (Cinemachine) и UI на своих сценах.

## Зачем

Две Main Camera (Boot + Game) → конфликт AudioListener, clear flags, stack.  
Одна Main Camera + Brain + VCam’ы → меню, подлёт к генератору, геймплей без смены «настоящей» камеры.

## Что где лежит

### Bootstrap (Root) — всегда

| Объект | Да / нет | Почему |
| --- | --- | --- |
| Main Camera | **да** | единственный вывод в кадр; `CinemachineBrain` + `AudioListener` |
| AudioListener | **да** (на Main Camera) | один на игру |
| EventSystem | **да** | UI input глобально |
| Startup | **да** | composition / AppFlow |
| Canvas меню | **нет** | на сцене MainMenu |
| Directional Light | **нет** | свет мира/меню — у тех сцен |
| Уровень / NPC | **нет** | только Game |

### MainMenu — additive, пока в оболочке меню

| Объект | Да / нет |
| --- | --- |
| Canvas (Press Any Key, Main Menu, кнопка Start) | **да** |
| ShellUiBinder | **да** |
| VCam меню (опционально) | да, когда будет 3D-фон |
| Свет только для меню-декора | по необходимости |
| Main Camera | **нет** |

### Game — сессия

| Объект | Да / нет |
| --- | --- |
| Enviroment / Humans / … | **да** |
| Directional Light (+ volume) | **да** |
| VCam геймплея (и катсценные VCam) | **да** |
| SubScene Simulation | **да** |
| Main Camera | **нет** — смотрит Root-камера через Brain на активный VCam |

## Cinemachine

Пакет: `com.unity.cinemachine` **3.x** (в проекте есть).

| Где | Компонент |
| --- | --- |
| Bootstrap Main Camera | `CinemachineBrain` + `AudioListener` |
| Game | `VCam_Gameplay` (`CinemachineCamera`) + `RTSCameraTarget`. **Без** Main Camera |
| MainMenu | позже VCam меню при 3D-фоне |

До VCam на меню Overlay UI достаточно Root-камеры (чистый/нейтральный clear).

### RTS-риг на Game

Brain на Root смотрит на виртуальную камеру. Сама VCam не рендерит — она задаёт позу и линзу.

| Объект | Роль |
| --- | --- |
| `RTSCameraTarget` | точка, вокруг которой орбита; пан WASD двигает её по земле. Старт: плаза `(0, 2, 0)` |
| `VCam_Gameplay` | `CinemachineCamera` + `CinemachineOrbitalFollow` + `CinemachineHardLookAt` + `CinemachineInputAxisController` + `RTSCameraController` |

Управление:

| Ввод | Что делает |
| --- | --- |
| WASD | пан цели по XZ относительно взгляда |
| Shift | спринт пан |
| колёсико | зум: радиус 14–42, угол по параболе 20°→75° |
| ПКМ / СКМ + мышь | орбита по yaw вокруг цели |

Лимит пана: круг `maxMapRadius = 75` вокруг `(0,0,0)` (внешнее кольцо города ≈ 59).  
Pitch от зума, не от мыши — иначе контроллер и ось Y орбиты дерутся каждый кадр.

`RTSCameraController` пока в `Assets/Scripts` (Assembly-CSharp, наследие Sample). Канон позже — Presentation.


## Play Mode vs Editor (обязательно)

| Режим | Что в Hierarchy | Что происходит |
| --- | --- | --- |
| **Play (канон)** | Достаточно **одной** Bootstrap | `Startup` грузит MainMenu → по Start грузит Game, выгружает MainMenu |
| **Edit уровня** | Bootstrap + Game (additive) | Видишь мир через Brain+VCam |
| **Edit UI** | Bootstrap + MainMenu | Правишь Canvas |

`LoadSceneAsync` в Play **требует**, чтобы сцена была в **Build Profiles / Build Settings**.  
Иначе: «couldn't be loaded… not added to build profile» — AppFlow не стартует. Это не баг архитектуры.

Build list (порядок):
1. `Bootstrap` (index 0 — стартовая)
2. `MainMenu`
3. `Game`

SampleScene из билда убрать.



## Итог одной фразой

**Bootstrap = вечный глаз и нервная система (камера, input, startup). MainMenu = экраны. Game = мир и его свет/VCam.**

---

Связанные: [[02 Scenes & Lifetime]] · [[10 Vertical Slice — Shell + ECS Walkers]] · [[../GDD/08 UI & Visual|GDD UI]]
