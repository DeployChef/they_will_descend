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
| Нет App FSM | ❌ Нужен явный state machine |
| Tick FSM из `Startup.Update` | ❌ Стейты Enter/Exit; `Startup` без Update |

**Элегантная замена «директора»:** три узких роли вместо одного толстого.

---

## 1. Закон двух машин

| Машина | Технология | Знает |
| --- | --- | --- |
| **Shell** | обычный C# + UI + сцены | FSM потока, загрузка, input, FMOD-хост |
| **Simulation** | ECS | дни, ресурсы, рабочие, стройка |

Shell включает сессию (`SimClockCommand.InGame`). ECS не знает меню.  
Контейнер DI **не обязателен**. Сначала инспектор-ссылки + конструкторы из `TheyWillDescend.Main`.  
Сборки: [[01 Folder Structure]] — Shell-код в Presentation, вход в Main.

**Нет `SimGate`.** Желаемый тик — поля на `SimControl`. UI пишет `SimClockCommand`.

---

## 2. Элегантное ядро Shell (канон)

```text
Bootstrap hosts (соседи, inspector refs, без GetComponent/AddComponent)
  Main Camera, EventSystem, Startup, GameAudio, GameInput, GameSession

Startup
  Awake → BootAsync (UniTask)
  грузит MainMenu, зовёт AppFlowFactory.Create, Start(PressAnyKey)
  нет Update

AppStateMachine
  владеет текущим IAppState
  только TransitionTo(stateId)

IAppState (PressAnyKey, MainMenu, LoadingGame, Playing)
  Enter / Exit   — без Tick
  меню-стейты читают PressAnyKeyScreen.Current / MainMenuScreen.Current в Enter

GameSession
  StartAsync / DisposeAsync / LoadMainMenuAsync
  load/unload контента рана; ждёт bake: SimWorld.TryGet

SceneLoader (узкий)
  LoadAdditive / Unload — без знания экономики

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
| Restart = ad-hoc Find | `session.DisposeAsync(); session.StartAsync()` |
| Opening зашит в Start | Стейты Cutscene / Briefing позже |
| Имя врёт («директор всего») | Имена = реальные роли |

---

## 3. Frostpunk → стейты

```text
Boot → PressAnyKey → MainMenu → (позже ScenarioSelect / Cutscene / Briefing)
  → LoadingGame (session.StartAsync: Loading + Game, unload MainMenu)
  → Playing
  → выход в меню: session.DisposeAsync → MainMenu
```

Пауза **часов** — не стейт приложения. Esc в Playing: сначала `BuildWidget.Current?.TryHandleEscape()`, иначе `SimClockCommand.TogglePause()`. FSM остаётся Playing.

Меню паузы (Save/Quit) — позже отдельный оверлей, не путать с Frozen.

| State | SimControl | Заметка |
| --- | --- | --- |
| PressAnyKey / MainMenu | Off (`SessionInGame = 0`) | экраны живы, пока загружен MainMenu |
| LoadingGame | Off | грузит Game, выгружает MainMenu |
| Playing | Running или Frozen | Frozen = PlayerPaused / BuildLocked |

---

## 4. Часы ↔ ECS

Истина — `SimControl` на session entity.

```text
SessionInGame, PlayerPaused, BuildLocked, Speed
Mode = Off | Frozen | Running   (считает ConsumeSimClockCommandsSystem)
DeltaTime = frame * Speed       (ApplySimDeltaTime; не ноль на паузе)
```

UI / стейты: `SimCommands.TryPost(SimClockCommand.…)` — не пишут поля напрямую.

Системы читают `IsRunning` / `DeltaTime`. Не `timeScale`.

| Режим | Смысл продукта |
| --- | --- |
| **Off** | рана нет / меню / загрузка |
| **Running** | город живёт |
| **Frozen** | ран есть, пауза посреди сессии |

Замки паузы: [[13 Time HUD and Save]].

---

## 5. Composition Root (без DI-контейнера)

```csharp
// Startup: inspector refs на GameAudio, GameInput, GameSession
// AppFlowFactory — new + Register, без Find UI
var fsm = new AppStateMachine();
fsm.Register(new PressAnyKeyState(fsm, input));
fsm.Register(new MainMenuState(fsm, input));
fsm.Register(new LoadingGameState(fsm, session, input));
fsm.Register(new PlayingState(input, audio));
```

Экраны меню биндятся в Awake на своих панелях (`Current`). Стейты читают в Enter, не кэшируют с boot.

`skipMenuToGameTemporarily` — debug: сразу LoadingGame, MainMenu не грузится. **По умолчанию выключен.**

Позже VContainer *может* регистрировать те же хосты. Контейнер = замена ручного new, не новая архитектура.

---

## 6. Сцены

| Сцена | Роль |
| --- | --- |
| Bootstrap | хосты + Main Camera. Без меню-canvas |
| MainMenu | splash/menu + `PressAnyKeyScreen` / `MainMenuScreen` |
| Loading | переход |
| Game | мир, HUD, SubScene Simulation |

---

## 7. Что переносим из gmtk всё же

- Вечный Root + additive session scene
- Audio на Root
- Явный «вход в ран» (стейты + `GameSession.StartAsync`)

## Что не переносим

Card Inject, Find soft-restart, timeScale-as-sim, толстый Director, DI в симуляцию, `SimGate` как C#-write-model часов.

---

## 8. Порядок дальше

1. Ядро есть: `Startup` + `GameSession` + `SimControl`, пауза часов в Playing.
2. Меню паузы / выбор сценария — позже.
3. VContainer — только если Composition Root станет невыносимым.

---

## 9. Анти-паттерны

- Толстый `GameDirector` «на всё»
- Симуляция тикает на брифинге
- `bool paused` на всё подряд
- VContainer ради VContainer
- AppFlow размазан по кнопкам без FSM
- Кэшировать меню-UI на boot и выгрузить MainMenu
- `Startup.Update` / Tick у FSM
- Грузить MainMenu только ради Find, если skip в Game

---

Связанные: [[02 Scenes & Lifetime]] · [[03 Core Systems]] · [[07 Mentorship & Learning]]
