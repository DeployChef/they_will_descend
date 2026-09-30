# 09 App Shell

← [[Index]] | [[../Home|Home]]

Сеньорский контракт оболочки. Цель — **элегантность большой игры**, не копирование джем-привычек.

Связано: [[02 Scenes & Lifetime]] · [[03 Core Systems]] · [[08 Production ECS]]

---

## 0. Честный вердикт по gmtk / «GameDirector»

То, что было в GMTK (Root scope, Director, `GameStartState`, pause keys) — **удобный jam-каркас**, не священный канон.

| Идея джема | Оценка для полной игры |
| --- | --- |
| Вечный Root + additive Game | ✅ Оставить |
| Тонкий оркестратор сцен | ✅ Оставить как **SceneLoader**, не как бог |
| Один `GameDirector`, который грузит сцену *и* знает opening *и* restart | ❌ Жиреет |
| VContainer everywhere | ⚪ Опционально позже; сейчас **Composition Root** без контейнера |
| Pause = `timeScale` | ❌ Часы в ECS: `SimControl` |
| Нет верхней FSM | ✅ Поток — `ShellService`: он включает сцены. Машина внутри Game — только если появится режим без смены сцены |
| Tick из `Startup.Update` | ❌ `Startup` только собирает контейнер и открывает меню или ран |

**Элегантная замена «директора»:** три узких роли вместо одного толстого.

---

## 1. Две стороны

| Сторона | Технология | Знает |
| --- | --- | --- |
| **Shell** | обычный C# + UI + сцены | какие сцены загружены, input, FMOD-хост |
| **Simulation** | ECS | дни, ресурсы, рабочие, стройка |

Сим-часы включает `GameRun` в сцене Game, когда сессия дошла до Ready. ECS не знает меню.  
Контейнер — VContainer в Main/Presentation. Сборки: [[01 Folder Structure]] — Shell-код в Presentation, вход в Main.

**Нет `SimGate`.** Желаемый тик — поля на `SimControl`. UI пишет `SimClockCommand`.

---

## 2. Элегантное ядро Shell (канон)

```text
Root hosts (соседи)
  Main Camera, EventSystem, Startup, RootLifetimeScope, GameAudio, GameInput

RootLifetimeScope
  родительский контейнер на Root, autoRun выключен
  Startup.Awake вызывает Build()
  AppContext и ShellService — синглтоны здесь; ShellService не MonoBehaviour
  AppContext.IsFirstStart после старта рана становится true
  дочерние scope: MainMenu, Loading, Game. Они резолвят родителя по типу, без кросс-сценной ссылки

ShellService
  EnterGame / ReturnToMenu / ShowLoading / HideLoading / OpenMainMenu
  только сцены. Не читает сейв, не зовёт Begin, не проверяет сессию

AppContext
  RequestLaunch кладёт RunLaunch
  IsFirstStart становится true в этот момент

GameSession (сцена Game)
  сама берёт Launch, когда сцена включилась, и зовёт Begin
  каталоги и сценарий в инспекторе этой сцены
  сцены не грузит
  StopPlay гасит живой ран сразу, Shutdown сбрасывает ECS
  если сброс не подтвердился — GameRun включается снова
  когда ран готов — HideLoading; если ран не собрался и Shutdown прошёл — ReturnToMenu

GameRun (scope сцены Game)
  Arm, когда Begin дошёл до Ready: SimClockCommand.InGame(true), игровой ввод, пауза
  Disarm, когда ран уходит
  это не машина состояний

SceneLoader (узкий)
  LoadAdditive / Unload — без знания экономики; вызывает только ShellService

GameInput
  клон TheyWillDescend.inputactions (инспектор: только этот asset)
  Menu/Proceed и Game/Pause — FindAction на клоне, не InputActionReference в сцене
  Menu: Proceed (anyKey + ЛКМ/ПКМ/СКМ + gamepad south)
  Game: Pause (Esc)
```

### Почему это лучше GameDirector

| GameDirector (джем) | Эта схема |
| --- | --- |
| Один класс копит обязанности | Обязанности разрезаны |
| Restart = ad-hoc Find | `ShellService.ReturnToMenu()`, затем `Start(RunLaunch)` |
| Opening зашит в Start | Режим без смены сцены (катсцена, брифинг) — позже, если понадобится |
| Имя врёт («директор всего») | Имена = реальные роли |

---

## 3. Поток продукта

```text
Root → MainMenu
  → кнопка зовёт AppContext.RequestLaunch и ShellService.EnterGame
  → Loading, пока грузится Game и выгружается MainMenu
  → GameSession.Begin, затем GameRun включает часы; сессия снимает Loading
  → выход в меню: Pause делает StopPlay и Shutdown, затем ShellService.ReturnToMenu
```

Сплэш и кнопки меню живут в сцене MainMenu (`MainMenuFlow`). Press any key играется один раз за процесс; `AppContext.IsFirstStart` это помнит.

Пауза **часов** — оверлей на Game, не смена сцены. Esc: сначала `BuildWidget.Current?.TryHandleEscape()`, иначе `PauseMenuScreen` + `SimClockCommand.PlayerPaused`.

Меню паузы (Continue / Save / Load / Main Menu) — оверлей на Game, не путать с Frozen.

| Момент | SimControl | Кто |
| --- | --- | --- |
| Меню, загрузка | Off (`SessionInGame = 0`) | ран ещё не вооружён |
| Ран готов | Running или Frozen | `GameRun.Arm`; Frozen = PlayerPaused / BuildLocked |
| Выход в меню | Off | `GameRun.Disarm` в `StopPlay`, затем `Shutdown` |

---

## 4. Часы ↔ ECS

Истина — `SimControl` на session entity.

```text
SessionInGame, PlayerPaused, BuildLocked, Speed
Mode = Off | Frozen | Running   (считает ConsumeSimClockCommandsSystem)
DeltaTime = frame * Speed       (ApplySimDeltaTime; не ноль на паузе)
```

UI / `GameRun`: `SimCommands.TryPost(SimClockCommand.…)` — не пишут поля напрямую.

Системы читают `IsRunning` / `DeltaTime`. Не `timeScale`.

| Режим | Смысл продукта |
| --- | --- |
| **Off** | рана нет / меню / загрузка |
| **Running** | город живёт |
| **Frozen** | ран есть, пауза посреди сессии |

Замки паузы: [[13 Time HUD and Save]].

---

## 5. Composition Root

```csharp
// Startup.Awake
rootScope.Build();
var shell = rootScope.Container.Resolve<ShellService>();
await shell.OpenMainMenu();
```

Корневой scope регистрирует `AppContext`, `ShellService`, `GameAudio`, `GameInput`. Scope сцены Game регистрирует `GameRun`, `GameSession`, `PauseMenuScreen`. Сплэш и кнопки меню ведёт `MainMenuFlow` в сцене MainMenu. Пауза в ране — `PauseMenuScreen` на Game.

Меню: **Start Game** / **Load** / **Start Debug**. Список допустимых `DifficultyProfile` и default принадлежат `ScenarioDefinition`, а не отдельным полям `GameSession`. Start Game берёт `DefaultScenario.DefaultDifficulty`, Debug — `DebugScenario.DefaultDifficulty`. Null default = prefab-default balance. Load = слот (кнопка серая, если файла нет).

`skipMenuToGameTemporarily` — debug: сразу ран, MainMenu не грузится. **По умолчанию выключен.**

---

## 6. Сцены

| Сцена | Роль |
| --- | --- |
| Root | хосты + Main Camera. Без меню-canvas |
| MainMenu | splash/menu + `PressAnyKeyScreen` / `MainMenuScreen` |
| Loading | переход: старт рана, load слота, выход в меню |
| Game | мир, HUD, SubScene Simulation |

---

## 7. Что переносим из gmtk всё же

- Вечный Root + additive session scene
- Audio на Root
- Явный «вход в ран» (`ShellService` + `GameSession`)

## Что не переносим

Card Inject, Find soft-restart, timeScale-as-sim, толстый Director, DI в симуляцию, `SimGate` как C#-write-model часов.

---

## 8. Порядок дальше

1. Ядро есть: `Startup` + `ShellService` + `GameSession` + `GameRun` + `SimControl`. Пауза часов — оверлей на Game.
2. Выбор сценария — позже.
3. Машина внутри Game — только если появится режим без смены сцены.

---

## 9. Анти-паттерны

- Толстый `GameDirector` «на всё»
- Симуляция тикает на брифинге
- `bool paused` на всё подряд
- VContainer ради VContainer
- Кнопки сами грузят сцены, мимо `ShellService`
- Кэшировать меню-UI на boot и выгрузить MainMenu
- `Startup.Update`
- Грузить MainMenu только ради Find, если skip в Game

---

Связанные: [[02 Scenes & Lifetime]] · [[03 Core Systems]] · [[07 Mentorship & Learning]]
