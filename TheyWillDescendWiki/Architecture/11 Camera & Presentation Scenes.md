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
| Main Camera | **да** | единственный вывод в кадр; позже `CinemachineBrain` |
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
| Game | `CinemachineCamera` (VCam_Gameplay), **без** Main Camera |
| MainMenu | позже VCam меню при 3D-фоне |

До VCam на меню Overlay UI достаточно Root-камеры (чистый/нейтральный clear).


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
