# 09 App Shell

← [[Index]] | [[../Home|Home]]

Контракт оболочки: как процесс поднимается, как сцены сменяют друг друга, кто стартует ран. История решений и отступлений — [[../Refactoring/08 Shell, Scenes & Audio|Рефакторинг 08]].

Связано: [[02 Scenes & Lifetime]] · [[03 Core Systems]] · [[06 FMOD Audio]] · [[11 Camera & Presentation Scenes]]

---

## 0. Три срока жизни

| Срок жизни | Что живёт | Где |
| --- | --- | --- |
| **Процесс** | навигация, переходы, экран загрузки, сейв, ввод, аудио-ядро, EventSystem | `AppScope` — корневой scope VContainer, не сцена |
| **Сцена** | камера, слушатель, свет, UI, звук этой сцены | `MainMenu`, `Game` — у каждой свой `LifetimeScope` |
| **Ран** | ECS-мир игровой сессии | живёт, пока загружена `Game` |

Модель — как `GameInstance` / `World` / `GameMode` в Unreal. Из неё три правила:

1. **Любая сцена — точка входа.** Play на `MainMenu.unity` или `Game.unity` работает сам. Boot-сцены нет.
2. **Процесс не знает сцен изнутри.** Он грузит сцену по имени и передаёт параметры запуска. Не резолвит объекты из чужого контейнера и не зовёт их `Begin` / `Shutdown`.
3. **Сцена не знает других сцен.** Обе знают только сервисы процесса.

Симуляция (ECS) — источник истины рана. Контейнер живёт только в Presentation и Main; ECS о нём не знает.

---

## 1. Процесс: AppScope

Префаб `Prefabs/App/AppScope.prefab` указан как Root Lifetime Scope в `Settings/VContainerSettings.asset` (Preloaded Assets). VContainer создаёт его один раз при первом построении любого scope, помечает `DontDestroyOnLoad` и делает родителем всех scope'ов сцен. Scope сцены родителя не задаёт.

Следствие: **в каждой контентной сцене обязан быть свой `LifetimeScope`**, иначе процесс не поднимется.

```csharp
public sealed class AppScope : LifetimeScope
{
    [SerializeField] InputActionAsset inputActions;
    [SerializeField] LoadingScreen loadingScreen;    // дочерний объект префаба
    [SerializeField] AudioCatalog audioCatalog;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<ShellService>(Lifetime.Singleton);
        builder.Register<TransitionService>(Lifetime.Singleton);
        builder.Register<LoadingTasks>(Lifetime.Singleton);
        builder.Register<SaveService>(Lifetime.Singleton);
        builder.Register<InputService>(Lifetime.Singleton).WithParameter(inputActions);
        builder.RegisterComponent(loadingScreen);
        builder.RegisterInstance(audioCatalog);
        builder.RegisterEntryPoint<AudioService>().AsSelf();
    }
}
```

Дети префаба: `EventSystem` (единственный на процесс; в сценах запрещён), `LoadingScreen` (overlay-canvas).

| Сервис | Роль |
| --- | --- |
| `ShellService` | Только навигация: `StartRun(launch)` (→ `Game`), `RestartRun(launch)` (→ текущая сцена рана, поэтому рестарт блокаута остаётся в блокауте), `GoToMenu()`, `TryTakeLaunch(out launch)`, `SplashShown`. Хранит параметры запуска между кнопкой и загрузкой. `GameSession` не знает |
| `TransitionService` | Единственный, кто грузит сцены. Конвейер перехода (§3), реестр teardown'ов сцены |
| `LoadingTasks` | Сцена сообщает «мне нужно время»: `Add(task, weight, label)`. Ошибка задачи = исключение |
| `LoadingScreen` | Визуал процесса: `Show` / `Hide` (асинхронные), `SetProgress`. Переживает выгрузку и загрузку сцен |
| `InputService` | Клон `TheyWillDescend.inputactions`. Карты `Menu` (Proceed) и `Game` (Pause). Какую карту включить, решает точка входа сцены |
| `SaveService` | Слот сейва. UI и `GameSession` получают его внедрением, не статикой |
| `AudioService` | Банки, музыка, снапшоты, UI-звук. См. [[06 FMOD Audio]] |

---

## 2. Сцены

| Сцена | Scope | Точка входа | Что включает на входе |
| --- | --- | --- | --- |
| `MainMenu` | `MainMenuLifetimeScope` | `MainMenuFlow` | карта ввода `Menu`, музыка `Menu`; сплэш один раз за процесс (`ShellService.SplashShown`) |
| `Game` | `GameLifetimeScope` | `GameEntry` | банки `GameWorld` и `GameSession.Begin` в `LoadingTasks`; по готовности часы, карта `Game`, меню паузы, музыка `Gameplay` |

Build list: `MainMenu` (0), `Game`. Переходы только Single-загрузкой: одновременно живёт ровно одна сцена, одна камера, один слушатель FMOD.

### 2.1. `GameEntry`

Обычный класс, entry point `GameLifetimeScope` (не компонент).

```text
Initialize
  teardown → TransitionService.RegisterTeardown(EndRun)
  launch   = меню (TryTakeLaunch) → #if UNITY_EDITOR настройка EditorRunLaunch на GameLifetimeScope → Normal
  LoadingTasks.Add(AcquireWorldBanks, 1, "Loading sounds…")
  LoadingTasks.Add(BeginRun(launch),  3, "Preparing the city…")

BeginRun:  GameSession.Begin(launch); провал → исключение; успех → Arm
Arm:       SimClockCommand.InGame(true), InputService.EnableGame, PauseMenuScreen.Use, Music.Set(Gameplay)
EndRun:    Disarm (меню паузы, ввод, InGame(false)) → GameSession.Shutdown; провал → Arm обратно
```

Редакторская настройка (`Normal` / `Debug` / `Slot`) лежит полем на `GameLifetimeScope` и передаётся через `WithParameter`. В билде её ветка вырезана: без параметров всегда `Normal`, отладочный запуск не уедет игрокам.

### 2.2. Меню

Кнопки: **Start Game** / **Load** / **Start Debug** → `ShellService.StartRun(launch)`. Допустимые `DifficultyProfile` и default принадлежат `ScenarioDefinition`: Start Game берёт `DefaultScenario.DefaultDifficulty`, Debug — `DebugScenario.DefaultDifficulty`. Load = слот; кнопка серая, если файла нет.

---

## 3. Потоки

```text
Play из MainMenu (и билд)
  AppScope поднимается → MainMenu → MainMenuFlow: карта Menu, музыка Menu

Меню → «Начать»
  ShellService.StartRun(launch) → TransitionService.Run("Game")
    1. LoadingScreen.Show
    2. teardown'ы текущей сцены (у меню их нет)
    3. Single-загрузка Game (меню выгружается само)
    4. GameEntry забирает launch, кладёт работу в LoadingTasks; конвейер ждёт WaitAll
    5. LoadingScreen.Hide

Play из Game напрямую (редактор)
  AppScope поднимается → GameEntry: launch не передан → редакторская настройка
  LoadingTasks получил работу без активного перехода → TransitionService сам показывает экран

Игра → «Главное меню» / «Перезапуск» / «Загрузить»
  ShellService.GoToMenu() / RestartRun(launch) → тот же конвейер
  teardown Game (EndRun) идёт под экраном, до выгрузки сцены
```

Правила конвейера:
- Второй переход во время текущего игнорируется (`Run` возвращает `false`), а не ставится в очередь.
- Teardown вернул `false` → переход отменяется, экран убирается, игрок остаётся в сцене.
- Задача `LoadingTasks` упала → откат в `MainMenu`. Если упало само меню — экран убирается, дальше откатываться некуда.

---

## 4. Часы ↔ ECS

Истина — `SimControl` на session entity.

```text
SessionInGame, PlayerPaused, BuildLocked, Speed
Mode = Off | Frozen | Running   (считает ConsumeSimClockCommandsSystem)
DeltaTime = frame * Speed       (ApplySimDeltaTime; не ноль на паузе)
```

UI и `GameEntry` пишут `SimCommands.TryPost(SimClockCommand.…)`, не поля напрямую. Системы читают `IsRunning` / `DeltaTime`. Не `timeScale`, нет `SimGate`.

| Режим | Смысл | Кто |
| --- | --- | --- |
| **Off** | рана нет: меню, загрузка | до `Arm` и после `Disarm` |
| **Running** | город живёт | `GameEntry.Arm` |
| **Frozen** | ран есть, пауза посреди сессии | `PlayerPaused` / `BuildLocked` |

Пауза часов — оверлей на Game, не смена сцены. Esc: сначала `BuildWidget.Current?.TryHandleEscape()`, иначе `PauseMenuScreen` (Continue / Save / Load / Main Menu) + `SimClockCommand.PlayerPaused`. Замки паузы: [[13 Time HUD and Save]].

---

## 5. Временное

- **`GameSession.Shutdown` как teardown перехода** — до Ф1 [[../Refactoring/07 Foundation Fixes|07]]. ECS-мир переживает загрузку сцен, поэтому прошлый ран чистится вручную под экраном. Когда ран станет «стереть мир и создать заново», teardown упростится.
- **`.Current`-синглтоны экранов** (`PauseMenuScreen.Current`, `BuildWidget.Current` и др.) живут внутри одной сцены — допустимо. Сервисы процесса к ним не обращаются.
- **Перезагрузка домена при входе в Play включена.** Если её выключат, статики (`SimWorld`, `UiSound`, `.Current`) нужно сбрасывать через `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`.

---

## 6. Анти-паттерны

- Толстый `GameDirector`, который грузит сцены, знает opening и restart
- Сервис процесса резолвит объекты сцены или зовёт её жизненный цикл
- Кнопки грузят сцены мимо `ShellService`
- Boot-сцена или хук `playModeStartScene` вместо корневого scope
- Additive-загрузка контентной сцены поверх другой (две камеры, два слушателя)
- `EventSystem` в сцене
- Пауза через `timeScale`; симуляция тикает в меню или на загрузке
- VContainer внутри `ISystem`

---

Связанные: [[02 Scenes & Lifetime]] · [[03 Core Systems]] · [[07 Mentorship & Learning]]
