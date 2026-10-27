# 08. Shell, сцены и аудио: любая сцена — точка входа

← [[07 Foundation Fixes|07 Фундамент]] | [[Index|План рефакторинга]] | [[../Home|Главная вики]]

Рефакторинг оболочки, структуры сцен и аудио. Цель: **разорвать зависимость сцен от Root**. Открыл `Game.unity`, нажал Play — стартует ран по умолчанию, без меню и без Root. Открыл `MainMenu.unity` — стартует меню. Путь старта в редакторе и в билде один и тот же.

Документ **меняет канон** [[../Architecture/09 App Shell|09 App Shell]], [[../Architecture/11 Camera & Presentation Scenes|11 Camera]] и правил `app-shell`, `camera-scenes`. В частности, правило «одна Main Camera на Root» заменяется на противоположное: камера у каждой сцены своя. Это не уточнение, а замена правила.

Порядок относительно [[07 Foundation Fixes|07]]: **shell раньше фундамента ECS**. Что из-за этого временно — в §7.

---

## 0. Принцип: три срока жизни

Модель взята из Unreal, где она отработана десятилетиями: `GameInstance` живёт весь процесс и существует, какую бы карту ни открыли; `World` живёт с картой; `GameMode` — с игровой сессией.

| Срок жизни | Что живёт | У нас |
| --- | --- | --- |
| **Процесс** | сейв, настройки, навигация, переходы, аудио-ядро, ввод, EventSystem | корневой scope проекта VContainer. Не сцена |
| **Сцена** | камера, свет, UI, звук этой сцены | `MainMenu`, `Game` — каждая со своим `LifetimeScope` |
| **Ран** | ECS-мир игровой сессии | живёт, пока загружена `Game` |

Три правила, из которых следует всё остальное:

1. **Любая сцена — законная точка входа.** Процесс поднимается сам до первой сцены, независимо от того, какую сцену открыли.
2. **Процесс не знает сцен изнутри.** Он грузит их по имени и передаёт параметры запуска. Он не достаёт объекты из чужого контейнера и не вызывает их методы жизненного цикла.
3. **Сцена не знает других сцен.** Game не знает про меню, меню не знает про Game. Обе знают только сервисы процесса.

---

## 1. Текущее состояние (диагноз)

### 1.1. Что где лежит

| Сцена | Объекты |
| --- | --- |
| `Root` | `Startup`, `RootScope`, `Main Camera` (`CinemachineBrain` + `StudioListener`), `EventSystem`, `GameInput`, `GameAudio`, `GlobalAmbienceManager`, `AudioZoneManager`, `AudioVisibilityChecker`, `ZoneAmbienceSfxScheduler` |
| `MainMenu` | Canvas, `PressAnyKey`, `MainMenuPanel`, `MainMenuFlow`, `MainMenuLifetimeScope`. Камеры нет |
| `Loading` | `LoadingCanvas`, `Veil`, `Status`, `LoadingLifetimeScope` |
| `Game` | мир, свет, VCam + `RTSCameraController`, HUD, `GameSession`, `GameLifetimeScope`, `TimeLightController`. Камеры нет |

Вне сцен уже сейчас: `UiAudioManager` создаёт сам себя через `RuntimeInitializeOnLoadMethod` и живёт с `DontDestroyOnLoad`. Это и есть целевой принцип, но применён к одному классу.

### 1.2. Связи, из-за которых Game не стартует без Root

1. **Scope.** `SceneLifetimeScope.Awake` жёстко ставит родителя: `ParentReference.Create<RootLifetimeScope>()`. Без Root `GameLifetimeScope` не собирается.
2. **Камера и уши.** Main Camera, Brain, `StudioListener`, `EventSystem`, `GameInput` есть только на Root. В Game лежит одна VCam — рендерить её некому, слушателя FMOD нет.
3. **Звук мира лежит в Root, хотя нужен только Game.** `AudioZoneManager`, `AudioVisibilityChecker`, `ZoneAmbienceSfxScheduler`, `GlobalAmbienceManager` (ещё и с `DontDestroyOnLoad`) находят `RTSCameraController` в Game через `FindFirstObjectByType` поверх границы сцен. `BuildingViewBoard` в Game находит `AudioZoneManager` в Root так же. Это главная скрытая связь.
4. **Shell управляет внутренностями игры.** `ShellService.ResolveFrom<GameSession>` ищет `LifetimeScope` в корневых объектах сцены Game, резолвит из её контейнера `GameSession` и сам зовёт `Begin` / `Shutdown`.
5. **Хост процесса читает ран.** `GameAudio` на Root в `LateUpdate` лезет в ECS за `SimControl.PlayerPaused`.
6. **Симптомы.** `Startup.OpenMenu` закрывает «лишние сцены, открытые в редакторе», `skipMenuToGameTemporarily` — отладочный обход меню. Оба существуют, потому что сцены несамостоятельны.

### 1.3. Аудио

- В настройках FMOD `BankLoadType: 0` — грузить все банки при старте (~120 МБ: `Main_theme` 58, `BUILDING` 31, `Ambience_Town` 28, `Ambience_Wind_Generator` 5, `UI` 0.2). Поверх этого `FmodBankLoader` вручную грузит банки с сэмплами, а `LoadBanksForEvent` ищет банк события перебором всех банков. Две политики одновременно.
- Пути событий, снапшотов и банков захардкожены строками в `GameAudio`, `UiAudioManager`, `GlobalAmbienceManager`, `BuildingConstructionAudio`. Правило вики [[../Architecture/06 FMOD Audio|06 FMOD]] — `EventReference` вместо строк — не соблюдается.
- Три способа существовать: компонент на Root (`GameAudio`), самосоздающийся синглтон (`UiAudioManager`), компонент на Root с `DontDestroyOnLoad` (`GlobalAmbienceManager`).
- Музыка: `GameAudio` стартует `event:/SOUNDTRACK/MUSIC_STATES` один раз и только ставит на паузу. Смены состояния между меню и игрой нет.

### 1.4. Что проверено и не является проблемой

- Перезагрузка домена при входе в Play **включена** (`EnterPlayModeOptions` включены, но ничего не отключено). Статики (`SimWorld`, `FmodBankLoader._loaded`, `UiAudioManager._instance`, все `.Current`) сбрасываются. Если перезагрузку домена когда-нибудь выключат ради скорости, это станет риском (§8).
- VContainer 1.19 — поддерживает корневой scope проекта. Сейчас не настроен: `VContainerSettings` в проекте нет.

---

## 2. Целевая архитектура

```text
ПРОЦЕСС — AppScope (префаб; VContainer создаёт сам, DontDestroyOnLoad, родитель всех сцен)
  ShellService        навигация: StartRun(launch) / RestartRun() / GoToMenu(); хранит параметры запуска
  TransitionService   единый конвейер переходов + экран загрузки + прогресс
  LoadingTasks        реестр работы, которую сцена делает под экраном загрузки
  AudioService        банки по наборам, музыкальный директор, снапшоты, шины/VCA, UI-звук
  SaveService, Settings
  InputService        клон .inputactions, карты Menu / Game
  EventSystem         единственный на процесс

СЦЕНА MainMenu — MainMenuScope
  Main Camera + CinemachineBrain + StudioListener, свет/фон меню
  MainMenuFlow, экраны, кнопки → ShellService
  на входе: AudioService.Music.Set(Menu), InputService.EnableMenu()

СЦЕНА Game — GameScope
  Main Camera + CinemachineBrain + StudioListener, VCam, свет, мир, HUD
  GameEntry           точка входа: берёт параметры запуска, стартует ран, включает часы/ввод
  GameSession         Begin / Shutdown рана (как сейчас, но его зовёт GameEntry, а не Shell)
  WorldAudio          аудио-ячейки, ветер, планировщик SFX, видимость; мост паузы
  на входе: AudioService.Banks.Acquire(GameBanks), Music.Set(Gameplay), InputService.EnableGame()

РАН — ECS DefaultWorld; ран = время жизни сцены Game
```

Исчезают: сцены `Root` и `Loading`, `Startup`, `RootLifetimeScope`, `LoadingLifetimeScope`, `SceneLifetimeScope`, `skipMenuToGameTemporarily`, `CloseLeftoverScenes`, `ResolveFrom`, `GameRun` (вливается в `GameEntry`).

Build list: `MainMenu` (индекс 0, стартовая в билде), `Game`.

### 2.1. Потоки

```text
Билд / Play из MainMenu
  AppScope поднимается → MainMenu грузится как первая сцена → меню

Меню → «Начать»
  ShellService.StartRun(launch)
    → TransitionService: показать экран → Single-загрузка Game (меню выгружается само)
    → GameEntry забирает launch → регистрирует подготовку в LoadingTasks
    → TransitionService ждёт LoadingTasks → убрать экран

Play из Game (напрямую в редакторе)
  AppScope поднимается → Game уже загружена → GameEntry: launch не передан
    → берёт редакторскую настройку запуска → та же подготовка через LoadingTasks
    → TransitionService сам показывает экран на первом кадре и убирает по готовности

Игра → «Главное меню»
  ShellService.GoToMenu()
    → показать экран → teardown Game (§7) → Single-загрузка MainMenu → убрать экран

Игра → «Перезапуск» / «Загрузить сейв»
  ShellService.RestartRun(launch) → тот же конвейер, целевая сцена снова Game
```

Один конвейер на все переходы. Экран загрузки показывает только `TransitionService`.

---

## 3. Процесс: AppScope

### 3.1. Как устроено в VContainer

- Создать ассет `VContainerSettings` (меню Create → VContainer → VContainer Settings) и указать в нём **Root Lifetime Scope** — префаб `AppScope`.
- VContainer сам создаёт этот префаб при первом построении любого `LifetimeScope`, помечает его `DontDestroyOnLoad` и делает родителем всех scope'ов без явного родителя.
- Ассет настроек должен попадать в билд через Preloaded Assets (VContainer добавляет его туда сам; проверить в Player Settings после создания).
- Scope'ы сцен **не** задают родителя вручную. Базовый класс `SceneLifetimeScope` с `ParentReference.Create<RootLifetimeScope>()` удаляется.
- Следствие: **в каждой контентной сцене обязан быть свой `LifetimeScope`**. Сцена без scope'а не поднимет AppScope. Для `MainMenu` и `Game` это уже так.

```csharp
public sealed class AppScope : LifetimeScope
{
    [SerializeField] AudioCatalog audioCatalog;
    [SerializeField] LoadingScreen loadingScreenPrefab;
    [SerializeField] InputActionAsset inputActions;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(audioCatalog);
        builder.Register<AudioService>(Lifetime.Singleton).AsSelf();
        builder.Register<ShellService>(Lifetime.Singleton);
        builder.Register<TransitionService>(Lifetime.Singleton);
        builder.Register<LoadingTasks>(Lifetime.Singleton);
        builder.Register<SaveService>(Lifetime.Singleton);
        builder.Register<InputService>(Lifetime.Singleton).WithParameter(inputActions);
        builder.RegisterComponentInNewPrefab(loadingScreenPrefab, Lifetime.Singleton).DontDestroyOnLoad();
        builder.RegisterEntryPoint<AppBoot>();   // старт аудио-ядра, первый показ экрана
    }
}
```

Пример иллюстрирует форму, а не финальный список. `EventSystem` — дочерний объект префаба `AppScope`.

### 3.2. Почему так, а не принудительная Boot-сцена

Распространённая альтернатива — редакторный хук `EditorSceneManager.playModeStartScene`, который всегда стартует Play с Boot-сцены, а та догружает открытую сцену. Плюс: путь старта совпадает с билдом. Минус: сцены остаются несамостоятельными, зависимость просто спрятана за хуком.

Корневой scope проекта даёт оба плюса: путь старта одинаков (процесс поднимается до первой сцены и в редакторе, и в билде), и сцены самостоятельны.

### 3.3. `AppContext`

Сейчас хранит только `IsFirstStart` (показан ли сплэш). Это состояние навигации — переезжает в `ShellService` как свойство «сплэш уже показан». Отдельный класс не нужен.

### 3.4. Граница DI остаётся прежней

Контейнер только в Presentation и Main. ECS о нём не знает. Сервисы процесса не читают ECS напрямую (§6.5).

---

## 4. Навигация и переходы

### 4.1. `ShellService` — только навигация

```text
StartRun(RunLaunch launch)
RestartRun(RunLaunch launch)
GoToMenu()
bool TryTakeLaunch(out RunLaunch launch)   // забирает и очищает; вызывает GameEntry
bool SplashShown { get; }
```

Не знает `GameSession`, не резолвит объекты сцен. Хранит параметры запуска между «нажали кнопку» и «Game загрузилась».

### 4.2. `TransitionService` — единый конвейер

```text
Run(target scene, teardown current)
  1. экран загрузки: показать (с анимацией входа), дождаться, пока закроет кадр
  2. teardown текущей сцены (§7) — под экраном
  3. Single-загрузка целевой сцены (allowSceneActivation по готовности экрана)
  4. дождаться LoadingTasks: всё, что сцена зарегистрировала при активации
  5. экран загрузки: убрать (с анимацией выхода)
```

- Только `TransitionService` грузит сцены. `SceneLoader` становится его внутренней деталью.
- Повторный переход во время текущего игнорируется или ставится в очередь — одно правило, а не `_busy` в каждом методе.
- **Без активного перехода** (Play с любой сцены в редакторе): если при активации сцены в `LoadingTasks` появилась работа, `TransitionService` сам показывает экран на первом кадре. Поэтому прямой запуск Game выглядит так же, как запуск из меню.

### 4.3. `LoadingTasks` — сцена сообщает, что ей нужно время

```csharp
// В точке входа сцены, при активации:
_loading.Add(PrepareRun(ct), weight: 3f, label: "Город");
_loading.Add(_audio.Banks.Acquire(gameBanks, ct), weight: 1f, label: "Звук");
```

- Задачи с весами складываются в общий прогресс для экрана загрузки.
- Ошибка задачи — повод для `TransitionService` вернуть игрока в меню с сообщением, а не повесить экран навсегда.
- Это заменяет нынешнее «Shell вызывает `GameSession.Begin` и ждёт результат». Shell не знает, что делает сцена, — только что она занята.

### 4.4. Экран загрузки — визуал процесса

Экран обязан пережить и выгрузку сцены, из которой уходим, и загрузку той, в которую идём. Поэтому он **принадлежит процессу**, а не меню и не игре.

- Сейчас: префаб `LoadingScreen` (Canvas, `DontDestroyOnLoad`, создаётся `AppScope`). Нынешние `Veil` и `Status` из `Loading.unity` переезжают туда.
- Интерфейс экрана: `Show()`, `Hide()` (оба асинхронные — анимации входа и выхода), `SetProgress(float, string)`.
- Рост до «как в Skyrim» (3D-модель, видео, подсказки) — это замена префаба. Если визуал станет 3D-диорамой со своей камерой, `TransitionService` будет грузить её служебной сценой. Владелец не меняется.
- **Честное ограничение Unity.** Отдельного потока рендера для экрана загрузки (как MoviePlayer в Unreal) нет. Активация сцены (`Awake` всех объектов, десериализация) идёт на главном потоке, и анимация в этот момент замрёт. Чем тяжелее активация, тем заметнее рывок; `Game.unity` весит 18 МБ — первый кандидат. Лечится дешёвой активацией, тяжёлой работой, размазанной по кадрам внутри `LoadingTasks`, и анимациями, которые переживают пропуск кадра (видео, шейдеры, время без масштаба). Задача на потом; архитектура её допускает.

---

## 5. Сцены: самостоятельность

### 5.1. `GameEntry` и редакторская настройка запуска

```text
GameEntry (компонент + entry point GameScope)
  [SerializeField, editor-only] EditorRunLaunch editorLaunch   // Normal / Debug / Load slot; опц. сценарий, сложность

  на активации сцены:
    launch = shell.TryTakeLaunch(out var l) ? l
           : #if UNITY_EDITOR → editorLaunch
           : RunLaunch.Normal
    loading.Add(session.Begin(launch, ct))
    по готовности: часы InGame, InputService.EnableGame, PauseMenuScreen.Use, Music.Set(Gameplay)
```

- Приоритет: параметры от меню → редакторская настройка → `Normal`.
- Редакторская настройка работает **только в редакторе**. В билде без параметров всегда `Normal`: отладочный запуск не уедет игрокам.
- Настройка лежит в сцене и общая для команды через git — это командный дефолт. Если начнутся конфликты в коммитах, личное переопределение переезжает в `EditorPrefs` (у каждого своё, в git не попадает). Не делать заранее.
- `GameRun` (`Arm` / `Disarm`) вливается в `GameEntry`.

### 5.2. Камера, слушатель, EventSystem, ввод

- **Каждая контентная сцена имеет одну Main Camera** с `CinemachineBrain` и `StudioListener`. Переходы — Single-загрузкой, поэтому одновременно живёт ровно одна камера и один слушатель.
- `MainMenu` получает свою камеру (сейчас меню рисуется камерой Root; без неё URP не рендерит кадр).
- **`EventSystem` — один, в `AppScope`.** В сценах `EventSystem` запрещён (иначе дубль и предупреждение Unity при каждой загрузке).
- `InputService` (нынешний `GameInput` без MonoBehaviour) живёт в процессе. Карты включает точка входа сцены, не процесс. `PauseMenuScreen` получает событие паузы через внедрение, как сейчас.

### 5.3. `.Current`-синглтоны экранов

`PauseMenuScreen.Current`, `BuildWidget.Current`, `ResearchWidget.Current`, `PauseCameraSwitch.Current` живут внутри одной сцены и умирают вместе с ней — это допустимо и не мешает самостоятельности. Переводить их на внедрение — отдельная гигиена, не часть этого рефакторинга. Единственное правило: сервисы процесса к ним не обращаются (сейчас обращается `GameRun`, он уходит в `GameEntry`).

---

## 6. Аудио

### 6.1. Три слоя по тем же срокам жизни

| Слой | Где | Что |
| --- | --- | --- |
| **Процесс** | `AudioService` в `AppScope` | банки по наборам, музыкальный директор, снапшоты, шины/VCA и громкости, UI-звук |
| **Сцена Game** | `WorldAudio` в `GameScope` | радиальные аудио-ячейки, ветер, планировщик SFX, видимость, звук стройки, мост паузы |
| **Сцена MainMenu** | `MainMenuScope` | атмосфера меню (если будет), cue музыки «меню» |

### 6.2. `AudioService` — фасад процесса

Обычный C#-класс, синглтон `AppScope`. Говорит на языке игры, а не FMOD:

```text
Banks.Acquire(AudioBankSet set, ct) → UniTask<IDisposable>   // счётчик ссылок; ждёт сэмплы
Music.Set(MusicCue cue)                                      // директор, §6.4
Snapshots.SetPaused(bool)                                    // ESC-снапшот
Volumes.Set(AudioChannel channel, float value)               // шины/VCA из настроек
Ui.Play(UiState state)                                       // нынешний UiAudioManager
```

`RuntimeManager` FMOD сам по себе уже глобальный и создаёт себя при первом обращении. `AudioService` — не замена ему, а слой правил поверх: кто грузит банки, кто владеет музыкой, какие имена у звуков.

### 6.3. DI или статический доступ — правило «один экземпляр, два входа»

Звук — классический случай, где глобальный доступ оправдан даже в AAA: API Wwise (`PostEvent`) статический, `RuntimeManager` FMOD — тоже. Звук нужен сотням мелких объектов, и протаскивать зависимость в каждую кнопку дороже, чем она стоит.

- **Владельцы звука получают `AudioService` через DI.** Сам сервис создаёт и уничтожает `AppScope`; каталог и настройки приходят в конструктор. `WorldAudio`, мост паузы, точки входа сцен — `[Inject]` от scope своей сцены.
- **Листья — через тонкий статический вход.** Кнопки и прочая мелочь: `UiSound.Play(UiState.Click)`. Он пересылает вызов в тот же экземпляр и молча ничего не делает, если сервиса нет (Edit Mode, тест). Второго экземпляра и логики внутри нет.
- **Объекты, созданные в рантайме** (вид здания, звук его стройки), получают зависимости от владельца — доски, которую уже внедрили. Внедрять в каждый спавненный префаб не нужно.

Правило владения: **кто создал `EventInstance`, тот его и освобождает.** Постоянные инстансы (музыка, снапшоты, UI-ивент) принадлежат `AudioService`. Звуки мира принадлежат объектам сцены Game и освобождаются в их `OnDestroy` — отдельного кода очистки при выходе в меню не нужно.

### 6.4. Музыкальный директор

Сцены не управляют музыкой напрямую. Они говорят директору, какая **тема** нужна:

```text
MusicCue { EventReference Event; string State; }   // в AudioCatalog: Menu, Gameplay, позже Crisis…

Music.Set(cue):
  то же событие, другое состояние → сменить параметр; переход делает FMOD (по такту, зонами перехода)
  другое событие                  → кроссфейд: старое ALLOWFADEOUT, новое start
  то же самое                     → ничего
```

Тогда вопрос «непрерывная музыка между меню и игрой или своя у каждой сцены» — решение звукорежиссёра в FMOD Studio, а не в коде. Судя по названию `MUSIC_STATES`, событие уже задумано под состояния. **Открытый вопрос к звукорежиссёру:** какие состояния есть у `MUSIC_STATES` и как называется параметр. Код от ответа не зависит.

Пауза: мост паузы в Game читает `SimControl.PlayerPaused` и вызывает `Snapshots.SetPaused`. Музыку на паузе директор не останавливает — это решает снапшот в FMOD (сейчас `GameAudio` ставит инстанс на паузу вручную; уточнить у звукорежиссёра, что задумано).

### 6.5. Мост ECS → звук живёт в сцене

```text
Game: SimPauseAudioBridge (LateUpdate) — читает SimControl → AudioService.Snapshots.SetPaused
```

Направление зависимостей: игра знает о звуке, звук не знает об игре. `AudioService` никогда не обращается к `SimWorld`.

### 6.6. Банки: наборы по сценам

- `AudioBankSet` — маленький ассет со списком банков (`[BankRef]` из FMOD, а не строки).
- **Процесс** при старте: `Master`, `Master.strings` (FMOD грузит сам), `UI`, `Main_theme` — набор `Core`, держится всегда.
- **Game** на входе: набор `GameWorld` = `Ambience_Town`, `Ambience_Wind_Generator`, `BUILDING`. Через `Banks.Acquire` внутри `LoadingTasks`: грузит банк, запускает загрузку сэмплов (`Bank.loadSampleData`) и ждёт её завершения (`getSampleLoadingState`). Выгрузка — при `Dispose` хэндла в teardown сцены.
- Настройки FMOD: `Load Banks` → **Specified** со списком набора `Core`. «Грузить всё при старте» убирается.
- Удаляются `FmodBankLoader.LoadBanksForEvent` / `GetBanksForEvent` (поиск банка перебором) и ручные `LoadBank` в `GameAudio`, `UiAudioManager`, `AudioZoneManager`.
- Счётчик ссылок нужен на будущее: если меню и игра когда-нибудь разделят банк, он не выгрузится на переходе.

### 6.7. `AudioCatalog` вместо строк

Один ассет, который правит звукорежиссёр:

- музыкальные cue (`Menu`, `Gameplay`);
- снапшоты (`Pause`);
- UI-событие и имя параметра состояний;
- события мира (ветер, город, стройка) — то, что сейчас захардкожено в `GlobalAmbienceManager` и `BuildingConstructionAudio`;
- наборы банков (`Core`, `GameWorld`).

Всё через `EventReference` / `[BankRef]`, которые переживают переименование в FMOD Studio.

### 6.8. Звук мира в Game

Переезжают с Root в Game под один объект `WorldAudio`:

| Было (Root) | Стало (Game) |
| --- | --- |
| `AudioZoneManager` | `WorldAudio/AudioZoneManager` |
| `AudioVisibilityChecker` | `WorldAudio/AudioVisibilityChecker` |
| `ZoneAmbienceSfxScheduler` | `WorldAudio/ZoneAmbienceSfxScheduler` |
| `GlobalAmbienceManager` (+ `DontDestroyOnLoad`) | `WorldAudio/WindAmbience` (без `DontDestroyOnLoad`) |

- Регистрируются в `GameScope` (`RegisterComponentInHierarchy`). Камеру (`RTSCameraController`), друг друга и `AudioService` получают через `[Inject]`.
- `BuildingViewBoard` получает `AudioZoneManager` через внедрение и передаёт его в `BuildingAudioSource` / `BuildingConstructionAudio` при создании вида.
- Центр города — из `CityGrid.Center` (ECS) или константа `(0,0,0)` по [[06 Headquarters & City Core|06]]; читать один раз при старте, не каждый кадр.
- Удаляются все `FindFirstObjectByType` в аудио и в `BuildingViewBoard`.
- Горячая перезагрузка банков в `AudioZoneManager` (`FindObjectsByType<BuildingAudioSource>`) остаётся только под `#if UNITY_EDITOR`.

---

## 7. Временное: shell раньше фундамента ECS

**Что временно.** Teardown сцены Game — асинхронный: `TransitionService` перед выгрузкой ждёт `GameSession.Shutdown` с фазами ECS (`Resetting → Unprepared`, despawn-команды).

**Почему.** Синхронная очистка мира появится только в Ф1 из [[07 Foundation Fixes|07]]. Сейчас ECS `DefaultWorld` переживает загрузку сцен, и без явного `Shutdown` дома и агенты прошлого рана остались бы в мире.

**Как сделано временно.** Сцена регистрирует свой teardown в процессе (условно `SceneTeardown.Register(Func<CancellationToken, UniTask>)`), `TransitionService` вызывает его на шаге 2 конвейера. `GameEntry` регистрирует туда `GameSession.Shutdown`.

**Правильная форма.** Ран = время жизни сцены Game. Мир очищается синхронно в `Dispose` `GameScope` (тег `RunScoped`, 07 §3.3). Реестр асинхронного teardown становится не нужен для рана (может остаться для сохранения при выходе, если появится).

**Когда заменяем.** Сразу после Ф1 из 07. Это одна точка — регистрация в `GameEntry`, а не логика, размазанная по проекту.

---

## 8. Фазы

Каждая фаза заканчивается рабочей игрой. Между фазами — коммит и чек-лист §9.

### Ф1. Процесс без сцены: AppScope

> **Статус: сделано.** Префаб `Prefabs/App/AppScope.prefab` (дети: GameInput, GameAudio, EventSystem), `Settings/VContainerSettings.asset` в Preloaded Assets. `GameInput` пока не переименован в `InputService` — это при Ф4.

1. Создать `VContainerSettings`, префаб `AppScope`; указать его как Root Lifetime Scope.
2. Перенести в `AppScope`: `ShellService` (с `SplashShown` вместо `AppContext`), `SaveService`, `InputService` (из `GameInput`), `GameAudio` (пока как есть), `EventSystem`.
3. Scope'ы сцен наследуются от `LifetimeScope` напрямую; `SceneLifetimeScope` удалить.
4. Root-сцена временно остаётся: на ней только `Startup` (открывает меню), `Main Camera` и звук мира. `RootLifetimeScope` удалить.

**Критерии:** Play с Root работает как раньше. В Hierarchy в Play есть `AppScope` под `DontDestroyOnLoad`. `rg "RootLifetimeScope|SceneLifetimeScope|AppContext"` — пусто.

### Ф2. Камеры и звук мира — в сцены

1. В Game: Main Camera + `CinemachineBrain` + `StudioListener`. В MainMenu: своя камера.
2. Удалить Main Camera с Root.
3. Звук мира переехать в `Game/WorldAudio` (§6.8), зависимости — через `GameScope`. Убрать `FindFirstObjectByType` в аудио и `BuildingViewBoard`.
4. Банки пока грузятся «всё при старте» — политику не трогаем.

**Критерии:** Play с Root → меню → игра: картинка и звук мира как раньше. Game, открытая отдельно в Edit Mode, показывает мир через свою камеру. `rg "FindFirstObjectByType" Presentation/Audio Presentation/City` — пусто.

### Ф3. Переходы: TransitionService + LoadingTasks

1. `TransitionService`, `LoadingTasks`, реестр teardown (§7), префаб `LoadingScreen` (из `Loading.unity`).
2. Переходы — Single-загрузкой. `ShellService` сводится к навигации (§4.1). `ResolveFrom` удалить.
3. `GameSession.Begin` вызывается из точки входа Game через `LoadingTasks`, а не из Shell.
4. Удалить `Loading.unity`, `LoadingLifetimeScope`, `CloseLeftoverScenes`.

**Критерии:** меню → игра → меню → игра → загрузка сейва → перезапуск без ошибок. Экран загрузки виден на всех переходах и не висит при ошибке старта рана (возврат в меню). `rg "ResolveFrom|CloseLeftoverScenes|GameScenes.Loading"` — пусто.

### Ф4. Самостоятельный Game: GameEntry, удаление Root

1. `GameEntry` с редакторской настройкой запуска (§5.1). `GameRun` влить в него.
2. Меню на входе включает свою карту ввода и музыкальное состояние; Game — свою.
3. Удалить `Root.unity`, `Startup`, `skipMenuToGameTemporarily`. Build list: `MainMenu` (0), `Game`.

**Критерии:**
- Открыть только `Game.unity` → Play → ран по редакторской настройке (Normal / Debug / слот), с экраном загрузки, часами, вводом, звуком.
- Открыть только `MainMenu.unity` → Play → сплэш и меню.
- Билд стартует с меню.
- `rg "Startup|skipMenuToGame|GameScenes.Root"` — пусто.

### Ф5. AudioService по-взрослому

1. `AudioCatalog`, `AudioBankSet` (`Core`, `GameWorld`), `AudioService` с банками, директором, снапшотами, громкостями, UI.
2. `GameAudio` и `UiAudioManager` вливаются в `AudioService`; статический вход `UiSound`.
3. Мост паузы в Game; `AudioService` не читает ECS.
4. Настройки FMOD → `Load Banks: Specified` (`Core`). Набор `GameWorld` — через `LoadingTasks` на входе в Game, выгрузка в teardown.
5. Захардкоженные пути → каталог.

**Критерии:**
- `rg "event:/|snapshot:/|bank:/" Presentation` — пусто (всё в каталоге).
- `rg "SimWorld|SimControl" Presentation/Audio` — пусто.
- В меню FMOD-профайлер показывает загруженными только `Core`; в игре — `Core` + `GameWorld`; после выхода в меню `GameWorld` выгружен.
- Музыка меняет состояние между меню и игрой по cue, без двойного инстанса.

### Ф6. Синхронизация вики и правил — §10.

---

## 9. Чек-лист после каждой фазы

1. Проект компилируется, консоль чистая на входе в Play.
2. Play из `MainMenu` (после Ф4) / из `Root` (до Ф4): сплэш → меню → «Начать» → ран.
3. В ране: поставить дом, назначить людей, пауза (звук паузы), Esc-меню.
4. Выйти в меню → снова в игру (без остановки Play). Нет дублей `EventSystem`, `StudioListener`, музыки.
5. Загрузить сейв из меню и из паузы.
6. После Ф4: Play только из `Game` — Normal, Debug, слот.
7. Звук мира: подлёт камеры к домам, ветер от высоты, стройка.
8. Коммит одной фазы.

---

## 10. Риски

- **Сцена без `LifetimeScope`** не поднимет `AppScope`. Правило: у каждой контентной сцены есть scope. Служебные тестовые сцены без scope'а работают без процесса — это осознанно.
- **Дубли на прямом Play с несколькими открытыми сценами.** Если в редакторе открыты MainMenu и Game вместе, обе стартуют одновременно: две камеры, два слушателя, две точки входа. Решение: при Play с несколькими контентными сценами `TransitionService` оставляет активную сцену редактора и выгружает остальные, с предупреждением в лог. Это не обход, а правило «одна контентная сцена».
- **ECS-мир переживает загрузку сцен.** До Ф1 из 07 очистку делает временный teardown (§7). Если teardown не дождались (исключение), прошлый ран останется в мире — `TransitionService` при ошибке teardown не грузит Game повторно, а уходит в меню.
- **Рывок анимации экрана загрузки** на активации тяжёлой `Game.unity` (§4.4).
- **Перезагрузка домена.** Сейчас включена. Если её отключат ради скорости входа в Play, все статики (`UiSound`, `SimWorld`, `.Current`, кэш банков) нужно сбрасывать через `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`.
- **Live Update FMOD** и горячая перезагрузка банков в редакторе должны пережить переход между сценами — проверить на Ф5.

---

## 11. Правки вики и правил (Ф6)

| Документ | Что исправить |
| --- | --- |
| `.cursor/rules/camera-scenes.mdc` | Переписать: нет Root; камера + Brain + `StudioListener` у каждой контентной сцены; `EventSystem` в `AppScope` |
| `.cursor/rules/app-shell.mdc` | Переписать: `AppScope` как корневой scope проекта; `ShellService` = навигация; `TransitionService` + `LoadingTasks`; `GameEntry`; нет `Startup` |
| [[../Architecture/09 App Shell\|09 App Shell]] | Новая схема процесса, потоки §2.1, композиция |
| [[../Architecture/02 Scenes & Lifetime\|02 Scenes]] | Сцены `MainMenu`, `Game`; build list; Single-загрузка |
| [[../Architecture/11 Camera & Presentation Scenes\|11 Camera]] | Камера у каждой сцены; таблица «что где лежит» |
| [[../Architecture/06 FMOD Audio\|06 FMOD]] | Слои аудио, `AudioService`, наборы банков, `Load Banks: Specified`, директор музыки |
| [[../Architecture/03 Core Systems\|03 Core Systems]] | Таблица служб Shell |
| [[../Architecture/01 Folder Structure\|01 Folder Structure]] | `Main` без `Startup`; папки `Presentation/Shell`, `Presentation/Audio` |

---

## 12. Контрольные вопросы

1. Почему экран загрузки принадлежит процессу, а не меню и не игре?
2. Что произойдёт, если в сцене нет `LifetimeScope`, и почему это важно для корневого scope проекта?
3. Почему `ShellService` не должен вызывать `GameSession.Begin`, и как тогда он узнаёт, что сцена готова?
4. Почему `AudioService` не читает `SimControl`, и где тогда живёт пауза звука?
5. Почему UI-кнопкам допустим статический `UiSound.Play`, а `WorldAudio` получает сервис через DI? Что общего у этих двух входов?
6. Кто освобождает `EventInstance` ветра при выходе в меню, и почему для этого не нужен отдельный код?
7. Почему редакторская настройка запуска не должна работать в билде?
8. Что в этом рефакторинге временно из-за того, что он идёт раньше 07, и в какой момент это заменяется?

---

Связанные: [[07 Foundation Fixes]] · [[../Architecture/09 App Shell|09 App Shell]] · [[../Architecture/11 Camera & Presentation Scenes|11 Camera]] · [[../Architecture/06 FMOD Audio|06 FMOD]]
