# 09 App Shell

← [[Index]] | [[../Home|Home]]

Сеньорский контракт оболочки. Цель — **элегантность большой игры**, не копирование джем-привычек.

Связано: [[02 Scenes & Lifetime]] · [[03 Core Systems]] · [[08 Production ECS]]

---

## 0. Честный вердикт по gmtk / «GameDirector»

То, что было в GMTK (Root scope, Director, `GameStartState`, pause keys) — **удобный jam-каркас**, не священный канон. Удобно дважды ≠ обязательно красиво для 3-летнего проекта.

| Идея джема | Оценка для полной игры |
| --- | --- |
| Вечный Root + additive Game | ✅ Оставить |
| Тонкий оркестратор сцен | ✅ Оставить как **SceneLoader**, не как бог |
| Один `GameDirector`, который грузит сцену *и* знает opening *и* restart | ❌ Жиреет; не элегантно |
| VContainer everywhere | ⚪ Опционально позже; сейчас **Composition Root** без контейнера |
| Pause = `timeScale` | ❌ Для ECS — **SimGate** |
| Нет App FSM | ❌ Для Frostpunk-потока нужен явный state machine |

**Элегантная замена «директора»:** не другое магическое имя, а **три узких роли** вместо одного толстого.

---

## 1. Закон двух машин

| Машина | Технология | Знает |
| --- | --- | --- |
| **Shell** | обычный C# + UI + сцены | FSM потока, сценарий, катсцена, пауза UI, SimGate |
| **Simulation** | ECS | дни, ресурсы, рабочие, законы |

Shell включает/выключает симуляцию. ECS не знает меню.  
Контейнер DI **не обязателен**. Сначала конструкторы из Composition Root (`TheyWillDescend.Main`).  
Сборки: [[01 Folder Structure]] — Shell-код в Presentation, вход в Main.

---

## 2. Элегантное ядро Shell (канон)

```text
CompositionRoot (Boot)
  создаёт порты и FSM один раз

AppStateMachine
  владеет текущим IAppState
  только TransitionTo(stateId) — тонкий

IAppState (PressAnyKey, MainMenu, LoadingGame, Playing; позже Cutscene / Briefing / Results / pause-menu)
  Enter / Exit / Tick(optional)
  меню-стейты читают ShellUiPort в Enter; не кэшируют UI с boot

GameSession
  Start(SessionConfig) / Dispose
  «одна попытка сценария»: load/unload контента рана, сброс

SimGate
  Off | Running | Frozen + Speed (x1/x2/x3) + PlayerPaused + BuildLocked
  владелец желаемого тика; ECS только зеркалит EffectiveMode/Speed
  единственный мост к ECS-тикам (не timeScale)
  Слот save/load — [[13 Time HUD and Save]]

SceneLoader (узкий)
  LoadAdditive / Unload — без знания экономики

ShellUiPort
  Current биндится сценой MainMenu (OnEnable/OnDisable)
  стейт читает порт в Enter; не держит ссылку с boot
```

### Почему это лучше GameDirector

| GameDirector (джем) | Эта схема |
| --- | --- |
| Один класс копит обязанности | Обязанности разрезаны |
| Restart = ad-hoc Find | `session.Dispose(); session.Start(config)` |
| Opening зашит в Start | Стейты Cutscene / Briefing |
| Сложно тестировать | Стейты и SimGate тестируются отдельно |
| Имя врёт («директор всего») | Имена = реальные роли |

«Director» как слово можно не использовать. Если понадобится фасад для UI-кнопки «New Game» — тонкий `IGameSessionFrontend`, не бог.

---

## 3. Frostpunk → стейты

```text
Boot → PressAnyKey → MainMenu → (позже ScenarioSelect / Cutscene / Briefing)
  → LoadingGame (session.Start) → Playing
  → выход в меню: session.Dispose → MainMenu
```

Пауза **часов** — не стейт приложения. Esc / ⏸ в Playing зовут `SimGate.TogglePlayerPause()`, FSM остаётся Playing.  
Меню паузы (Save/Quit) — позже отдельный оверлей или стейт, не путать с Frozen.

| State | SimGate | Заметка |
| --- | --- | --- |
| PressAnyKey / MainMenu | Off | `IShellUi` жив, пока загружен MainMenu |
| LoadingGame | Off | грузит Game, выгружает MainMenu (порт UI умирает) |
| Playing | Running или Frozen | Frozen = PlayerPaused / BuildLocked, не другой `IAppState` |

---

## 4. SimGate ↔ ECS

`SimGate` (C#, Presentation/Shell) хранит **желаемый** режим.  
Composition root (`Main.Startup`) каждый кадр зовёт `SimGate.PushClock` → `SimIo.SetClock` на испечённый session entity.  
Симуляционные systems читают только `SimControl`. Не `timeScale`.

| Режим | Смысл продукта |
| --- | --- |
| **Off** | рана нет / ещё не начали / меню |
| **Running** | город живёт |
| **Frozen** | ран есть, пауза посреди сессии |

---

## 5. Composition Root (без DI-контейнера)

```csharp
// Main.Startup — грузит MainMenu только если не skip
// Main.AppFlowFactory — new + Register, без Find
var simGate = new SimGate();
var session = new GameSession(scenes, coroutineHost);
var fsm = new AppStateMachine();
fsm.Register(new PressAnyKeyState(fsm, simGate, intents));
fsm.Register(new MainMenuState(fsm, simGate));
fsm.Register(new LoadingGameState(fsm, simGate, session));
fsm.Register(new PlayingState(simGate, intents));
```

Порт меню: `ShellUiBinder` биндит `ShellUiPort` в OnEnable. Стейты читают `Current` в `Enter`, не кэшируют с boot.  
`skipMenuToGameTemporarily` **не** грузит MainMenu — LoadingGame не требует UI.

Позже, если граф разрастётся — VContainer *может* регистрировать те же порты. Контейнер = замена ручного new, не новая архитектура.  
gmtk scopes имеют смысл, когда много hierarchy inject; **не** когда write model в ECS.

---

## 6. Сцены

| Сцена | Роль |
| --- | --- |
| Boot/Root | `Startup`, FSM, SimGate, Audio (позже). Без меню-canvas |
| MainMenu | splash/menu + `ShellUiBinder` → `ShellUiPort` |
| Game | мир, HUD, SubScene Simulation |

---

## 7. Что переносим из gmtk всё же

- Вечный Root + additive session scene  
- Audio на Root  
- Идея ref-counted keys для *нескольких presentation-пауз* (опционально поверх SimGate)  
- Явный «вход в ран» (у нас — стейты + `GameSession.Start`)

## Что не переносим

Card Inject, Find soft-restart, timeScale-as-sim, толстый Director, DI в симуляцию.

---

## 8. Порядок внедрения

1. Ядро есть: `Startup` + `GameSession` + `SimGate`, пауза часов в Playing.  
2. Выключить `skipMenuToGameTemporarily`, когда пойдёте чинить поток меню.  
3. Меню паузы / сценарии — позже.  
4. VContainer — только если Composition Root станет невыносимым.

---

## 9. Анти-паттерны

- Толстый `GameDirector` «на всё»  
- Симуляция тикает на брифинге  
- `bool paused` на всё подряд  
- VContainer ради VContainer  
- AppFlow размазан по кнопкам без FSM  
- Кэшировать `IShellUi` на boot и выгрузить MainMenu  
- Грузить MainMenu только ради Find, если skip в Game  

---

Связанные: [[02 Scenes & Lifetime]] · [[03 Core Systems]] · [[07 Mentorship & Learning]]
