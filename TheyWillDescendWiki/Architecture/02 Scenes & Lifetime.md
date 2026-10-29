# 02 Scenes & Lifetime

← [[01 Folder Structure]] | [[Index]] | Далее → [[03 Core Systems]]

Полный контракт оболочки: [[09 App Shell]]. Камеры: [[11 Camera & Presentation Scenes]].

## Сцены (канон)

| Сцена | Живёт | Содержимое |
| --- | --- | --- |
| **MainMenu** | пока в меню | Main Camera (+ `StudioListener`), Canvas: `PressAnyKeyScreen`, `MainMenuScreen`; `MainMenuFlow`, `MainMenuLifetimeScope` |
| **Game** | сессия рана | Main Camera (+ `CinemachineBrain`, `StudioListener`), VCam, уровень, свет, HUD, `WorldAudio`, `GameSession`, `GameLifetimeScope` |

Сцен Root и Loading нет. Процесс — префаб `AppScope` (корневой scope VContainer), экран загрузки — его дочерний объект.

Build list: `MainMenu` (0, стартовая в билде), `Game`.

Поток:

```text
AppScope (процесс, DontDestroyOnLoad)
  → MainMenu                                   // сплэш и кнопки крутит сцена меню
  → «Начать» → экран загрузки → Single-загрузка Game → GameEntry стартует ран
  → Playing (пауза часов = SimControl, не другой app-state)
  → выход в меню → экран → teardown рана → Single-загрузка MainMenu
```

Любая сцена — точка входа: Play на `Game.unity` стартует ран по полям `editorSaveId` / `editorDebug` на `GameLifetimeScope`.

Меню **не** содержит игровой мир. Game появляется только по Start.

## Lifetime

| Срок жизни | Владелец |
| --- | --- |
| Процесс | `AppScope`: Shell-сервисы, ввод, аудио-ядро, сейв, EventSystem |
| Сцена | `LifetimeScope` сцены: камера, UI, звук сцены |
| Ран | ECS DefaultWorld; ран = время жизни сцены Game |

Сцены грузятся только Single-загрузкой через `ShellService`. Additive поверх контентной сцены — нет.

Контейнер — только Presentation/Main, не ECS. Объекты сцены получают зависимости внедрением от scope своей сцены, не `Find*`.

## Часы рана (не отдельный стейт)

Off, пока нет сессии · Running в Playing · Frozen, если `PlayerPaused` или `BuildLocked` (стейт остаётся Playing). Speed (x1/x2/x3) на том же `SimControl`.  
Истина — ECS. UI пишет `SimClockCommand`. [[09 App Shell]] · тулза и save: [[13 Time HUD and Save]].

---

Связанные: [[11 Camera & Presentation Scenes]] · [[09 App Shell]]
