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
Контейнер DI **не обязателен**. Сначала конструкторы из Composition Root.

---

## 2. Элегантное ядро Shell (канон)

```text
CompositionRoot (Boot)
  создаёт порты и FSM один раз

AppStateMachine
  владеет текущим IAppState
  только TransitionTo(stateId) — тонкий

IAppState (Boot, Menu, Cutscene, Briefing, Playing, Paused, Results…)
  Enter / Exit / Tick(optional)
  каждый стейт сам ставит SimGate и шлёт UI

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
Boot → PressAnyKey → MainMenu → ScenarioSelect
  → Cutscene → Briefing → (session.Start) → Playing ⇄ Paused
  → Results → Menu | Restart (Dispose + Start)
```

| State | SimGate | Заметка |
| --- | --- | --- |
| Menu / Select | Off | ECS может не существовать |
| Cutscene / Briefing | Off | Session уже можно Start (сцена/сабсцена), тиков нет |
| Playing | Running | экономика идёт |
| Paused | Frozen | UI паузы; не путать с Briefing |
| Results | Off или Frozen | по дизайну |

---

## 4. SimGate ↔ ECS

`SimGate` (C#) хранит **желаемый** режим.  
`SimControlSyncSystem` (Shell, `SystemBase`) копирует его в singleton `SimControl`.  
Симуляционные systems читают только `SimControl` — Shell **не** пишет в `EntityManager` напрямую.

| Режим | Смысл продукта |
| --- | --- |
| **Off** | рана нет / ещё не начали / меню |
| **Running** | город живёт |
| **Frozen** | ран есть, пауза посреди сессии |

---

## 5. Composition Root (без DI-контейнера)

```csharp
// псевдокод Boot
var simGate = new SimGate();
var scenes = new SceneLoader();
var session = new GameSession(scenes, simGate);
var fsm = new AppStateMachine();
fsm.Register(new BriefingState(fsm, simGate, /* ui */));
fsm.Register(new PlayingState(fsm, simGate));
fsm.Register(new PausedState(fsm, simGate));
fsm.Start(AppStateId.Briefing); // stub: сразу в упрощённый поток
```

Позже, если граф разрастётся — VContainer *может* регистрировать те же порты. Контейнер = замена ручного new, не новая архитектура.  
gmtk scopes имеют смысл, когда много hierarchy inject; **не** когда write model в ECS.

---

## 6. Сцены

| Сцена | Роль |
| --- | --- |
| Boot/Root | CompositionRoot, FSM, SimGate, Audio (позже), loading |
| Game | камера, HUD, SubScene Simulation |
| MainMenu | рано можно UI-панелями на Boot |

Сейчас: `Bootstrap` ≈ зародыш Game; stub Shell можно повесить прямо на него.

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

1. **Stub без DI** (сейчас): `SimGate` + `AppStateMachine` + Briefing → Playing → Paused; дни только в Playing.  
2. `GameSession` когда появится load/unload.  
3. Настоящее меню / сценарии.  
4. VContainer — только если Composition Root станет невыносимым.

---

## 9. Анти-паттерны

- Толстый `GameDirector` «на всё»  
- Симуляция тикает на брифинге  
- `bool paused` на всё подряд  
- VContainer ради VContainer  
- AppFlow размазан по кнопкам без FSM  

---

Связанные: [[02 Scenes & Lifetime]] · [[03 Core Systems]] · [[07 Mentorship & Learning]]
