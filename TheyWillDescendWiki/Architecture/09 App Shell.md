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
        builder.Register<AppContext>(Lifetime.Singleton);
        builder.Register<ShellService>(Lifetime.Singleton);
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
| `AppContext` | Факты процесса: `CurrentLaunch` (запись при старте и рестарте, чтение не стирает), `SplashShown`. Не статика |
| `ShellService` | `StartRun(launch)` и `RestartRun(launch)` пишут билет и открывают `Game`. `GoToMenu()` открывает `MainMenu` и билет не трогает. Сам ведёт конвейер: шторка → teardown → Single-load → `LoadingScreen.Wait` → спрятать. `GameSession` не знает. Блокаут не знает |
| `LoadingScreen` | Шторка процесса и список работы сцены: `Add(task, weight, label)`, `Wait`. Упавшая задача бросает исключение. Переживает выгрузку и загрузку сцен |
| `InputService` | Клон `TheyWillDescend.inputactions`. Карты `Menu` (Proceed) и `Game` (Pause). Какую карту включить, решает точка входа сцены |
| `SaveService` | Слот сейва. UI и `GameSession` получают его внедрением, не статикой |
| `AudioService` | Банки, музыка, снапшоты, UI-звук. См. [[06 FMOD Audio]] |

---

## 2. Сцены

| Сцена | Scope | Точка входа | Что включает на входе |
| --- | --- | --- | --- |
| `MainMenu` | `MainMenuLifetimeScope` | `MainMenuFlow` | карта ввода `Menu`, музыка `Menu`; сплэш один раз за процесс (`AppContext.SplashShown`) |
| `Game` | `GameLifetimeScope` | `GameEntry` | банки `GameWorld` и `GameSession.Begin` на `LoadingScreen`; по готовности часы, карта `Game`, меню паузы, музыка `Gameplay` |

Build list: `MainMenu` (0), `Game`. Переходы только Single-загрузкой: одновременно живёт ровно одна сцена, одна камера, один слушатель FMOD.

### 2.1. `GameEntry`

Обычный класс, entry point `GameLifetimeScope` (не компонент).

```text
Initialize
  teardown → ShellService.RegisterTeardown(EndRun)
  launch   = AppContext.CurrentLaunch → #if UNITY_EDITOR поля на GameLifetimeScope (save id или New+debug) → New
  LoadingScreen.Add(AcquireWorldBanks, 1, "Loading sounds…")
  LoadingScreen.Add(BeginRun(launch),  3, "Preparing the city…")

BeginRun:  GameSession.Begin(launch); провал → исключение; успех → Arm
Arm:       SimClockCommand.InGame(true), InputService.EnableGame, PauseMenuScreen.Use, Music.Set(Gameplay)
EndRun:    Disarm (меню паузы, ввод, InGame(false)) → GameSession.Shutdown; провал → Arm обратно
```

Редакторская настройка — два поля на `GameLifetimeScope`: `editorSaveId` и `editorDebug`. Пустой id даёт `RunLaunch.New(editorDebug)`, заполненный — `RunLaunch.FromSave(id)`. В билде ветка вырезана: без билета из меню всегда `New`.

### 2.2. Меню

Кнопки: **Start Game** / **Load** / **Start Debug** → `ShellService.StartRun(launch)`. Start Game — `RunLaunch.New()`, Debug — `RunLaunch.New(debug: true)`, Load — `RunLaunch.FromSave(SaveService.Slot0)`. Допустимые `DifficultyProfile` и default принадлежат `ScenarioDefinition`: Start Game берёт `DefaultScenario.DefaultDifficulty`, Debug — `DebugScenario.DefaultDifficulty`. Кнопка Load серая, если файла `run_slot0.json` нет.

---

## 3. Потоки

```text
Play из MainMenu (и билд)
  AppScope поднимается → MainMenu → MainMenuFlow: карта Menu, музыка Menu

Меню → «Начать»
  ShellService.StartRun(launch) пишет AppContext.CurrentLaunch и грузит Game
    1. LoadingScreen.Show
    2. teardown'ы текущей сцены (у меню их нет)
    3. Single-загрузка Game (меню выгружается само)
    4. GameEntry читает CurrentLaunch, кладёт работу на LoadingScreen; конвейер ждёт Wait
    5. LoadingScreen.Hide

Play из Game напрямую (редактор)
  AppScope поднимается → CurrentLaunch пуст → редакторская настройка
  LoadingScreen получил работу без активного перехода → ShellService сам показывает экран

Игра → «Главное меню» / «Перезапуск» / «Загрузить»
  ShellService.GoToMenu() / RestartRun(launch) → тот же конвейер. GoToMenu билет не стирает
  teardown Game (EndRun) идёт под экраном, до выгрузки сцены
```

Правила конвейера:
- Второй переход во время текущего игнорируется (метод возвращает `false`), а не ставится в очередь. Билет при этом не перезаписывается.
- Teardown вернул `false` → переход отменяется, экран убирается, игрок остаётся в сцене.
- Задача на `LoadingScreen` или сама загрузка сцены упала → исключение. В меню из-за этого не уходим.

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
