# 09 App Shell

← [[Index]] | [[../Home|Home]]

Сеньорский контракт **оболочки приложения** (всё вокруг симуляции): меню, сценарии, катсцены, пауза, старт рана.  
Собрано из: Frostpunk-потока, текущего ECS-ядра, и переиспользуемого shell из **gmtk_2026** (без card-архитектуры).

Связано: [[02 Scenes & Lifetime]] · [[03 Core Systems]] · [[08 Production ECS]]

---

## 1. Закон двух машин

| Машина | Технология | Знает |
| --- | --- | --- |
| **Shell** | C# + UI + сцены + **VContainer** | меню, FSM потока, какой сценарий, катсцена, пауза UI, SimGate |
| **Simulation** | **ECS / Entities** | дни, ресурсы, рабочие, законы, win/lose факты |

> Shell **включает/выключает** симуляцию.  
> Симуляция **не знает** про кнопки меню и VContainer.

DI — в Shell/Presentation. В `ISystem` контейнер **не** инжектим.

---

## 2. Что берём из gmtk_2026

Джем: вечный Root → additive Game → child scope → тонкий director → opening под pause.  
Это хороший **каркас сцен/DI**, плохая **модель симуляции** для полной игры.

### Переносим

| Паттерн | Зачем |
| --- | --- |
| Вечная **Root** + additive **Game** | глобальные сервисы переживают ран |
| `RootLifetimeScope` + `GameLifetimeScope`, **Auto Run off**, `Build()` в коде | контролируемый lifecycle |
| Parent scope проставляется директором после load | надёжнее cross-scene Inspector |
| Тонкий **`IGameDirector`** | только сцены/сессия, не экономика |
| **FMOD / Audio** на Root | переживает выгрузку Game |
| Ref-counted pause keys (идея) | несколько оверлеев не дерутся за «паузу» |
| Явный «вход в сессию» (`GameStartState` в джеме) | у нас разворачивается в полный **AppFlow FSM** |
| Asmdef: Core contracts / Main(shell) / Presentation | границы сборок |

### Не переносим

| Джем | Почему нет |
| --- | --- |
| Card DnD + Inject всех зданий/карт | другой жанр |
| `FindObjectsByType` + manual Inject | jam-hack |
| Симуляция через MonoBehaviour + `ITickable` + `timeScale` | у нас ECS + **SimGate** |
| Нет Main Menu / App FSM | полной игре нужен Frostpunk-поток |
| Soft restart через `GameObject.Find` | нужен явный Session Reset API |
| Economy/session services в том же DI, что UI «на всё» | write model — ECS |

Референс-код джема (смотреть, не копировать слепо):  
`gmtk_2026/.../Main/Startup.cs`, `GameDirector.cs`, `DI/RootLifetimeScope.cs`, `DI/GameLifetimeScope.cs`, `GameAppStates/GameStartState.cs`.

---

## 3. Frostpunk-поток → AppFlow FSM

Целевые состояния Shell (имена можно уточнить, смысл — нет):

```text
Boot
  → PressAnyKey
  → MainMenu
  → ScenarioSelect
  → ScenarioIntroCutscene
  → ScenarioBriefing          // текст + «Начать»
  → EnterGameplay             // load Game, камера к генератору/пирамиде
  → Playing                   // SimGate.Running
       ⇄ Paused               // SimGate.Frozen + pause UI
  → Results (Win/Lose)
  → (QuitToMenu | RestartScenario)
```

`IAppFlow` / `AppFlowController` владеет текущим состоянием и переходами.  
Каждый стейт: какие экраны видны + какой **SimGate** + какие input maps.

Это оркестрация уровня application (как workflow/saga на бэке), **не** ECS system.

---

## 4. SimGate — мост Shell ↔ ECS

Один переключатель, source of truth у Shell:

| Значение | Когда | Эффект |
| --- | --- | --- |
| **Off** | меню, катсцена, брифинг (до «Начать») | Simulation group не тикает / не считаем экономику |
| **Running** | Playing | `AdvanceGameTimeSystem` и остальные sim-системы идут |
| **Frozen** | Pause menu, часть синематиков поверх уже начатого рана | sim стоит, UI/оверлей живы |

### Реализация (контракт)

- Shell вызывает `ISimGate.Set(Off|Running|Frozen)`.
- Реализация гасит/включает **`SimulationSystemGroup`** (или наш custom group), либо выставляет singleton `SimControl`, который sim-системы требуют через `RequireForUpdate` / early-out.
- **Не** полагаться только на `Time.timeScale`: UI и камера часто должны жить при паузе; ECS-тики должны резаться явно.

Джем использовал `timeScale` + ref-count keys — идею ключей сохраняем для **presentation pause** (диалоги, оверлеи), а для экономики — **SimGate**.

Пока игрок в Briefing: мир/SubScene уже можно грузить (чтобы камера прилетела к печке), но SimGate = **Off**, пока не нажали «Начать».

---

## 5. Сцены

| Сцена | Живёт | Содержимое |
| --- | --- | --- |
| `Boot` / `Root` | всегда | Startup, RootLifetimeScope, Audio/FMOD, AppFlow, Director, Shell bus, loading/press-any-key, опционально pause overlay root |
| `MainMenu` | по необходимости | меню, выбор сценария (можно панелями на Root на раннем этапе) |
| `Game` | сессия рана | GameLifetimeScope, камера, HUD, CameraDirector, **SubScene Simulation** (ECS bake) |

Текущий учебный `Bootstrap.unity` + `SubScenes/Simulation.unity` → эволюционирует в **`Game` + Simulation SubScene**.  
Root появится, когда вырастет меню; до этого допустим временный stub AppFlow прямо на Bootstrap.

Build Settings: `#0 = Root`. Game/Menu — load additive из Director.

---

## 6. DI-карта (зрелая)

```
RootLifetimeScope
  IAppFlow
  IGameDirector
  ISimGate
  IShellEventBus          // UI/audio/flow — не write model города
  IAudioManager (FMOD)
  IGameLog (или static GameLog + optional wrapper)
  PauseOverlay (hierarchy)
  ScenarioCatalog (read-only content access)

GameLifetimeScope : child of Root
  SessionConfig (выбранный сценарий, seed)
  CameraDirector, HUD presenters
  SimulationBridge / EventRelay   // ECS → Shell bus
  НЕ регистрируем «EconomyService» как write model
```

**Simulation (ECS):** без VContainer.  
Commands: Presentation → buffer/singleton queue → systems.  
Facts: systems → event buffer → Relay → `IShellEventBus` → UI/FMOD.

---

## 7. Director vs AppFlow vs StartState

| Тип | Ответственность |
| --- | --- |
| **AppFlow** | «где мы в продукте» (Menu vs Briefing vs Playing) |
| **GameDirector** | load/unload сцен, Build/Dispose Game scope, передать SessionConfig |
| **SessionEnter** (наследник идеи `GameStartState`) | последовательность *внутри* входа в ран: cutscene → briefing → camera fly → SimGate.Running |

Director не считает еду. AppFlow не bake’ит здания. ECS не открывает Main Menu.

---

## 8. Пауза vs «ещё не начали»

| | Briefing / Cutscene | Esc Pause |
| --- | --- | --- |
| AppFlow state | ScenarioBriefing / Cutscene | Paused |
| SimGate | Off (или Frozen если уже Playing) | Frozen |
| UI | briefing / video | pause menu |
| Выход | «Начать» → Playing | Resume → Playing |

Не склеивать в один `bool paused`.

---

## 9. Логирование

Уже есть `GameLog` + `LogChannel` (`Scripts/Infrastructure/Logging`).  
Shell и Simulation пишут в каналы (`Bootstrap`, `Time`, …).  
В Burst — не логировать; факты → managed relay.

---

## 10. Порядок внедрения (чтобы не утонуть)

1. **Shell stub** на текущем Bootstrap: мини-FSM (Boot → Briefing → Playing) + SimGate гасит время до Playing.  
2. Вынести Root + VContainer, когда появятся меню/FMOD-сервисы как зависимости.  
3. MainMenu + ScenarioSelect.  
4. Cutscene / CameraDirector.  
5. Параллельно — домены ECS (ресурсы, рабочие) **всегда** за SimGate.

Урок экономики не отменяет shell: любой новый system уважает Running.

---

## 11. Анти-паттерны

- `AdvanceGameTimeSystem` тикает на экране меню  
- UI пишет в ECS stocks минуя commands  
- `IEconomyService` в VContainer как истина рана  
- Пауза = только `timeScale` без SimGate  
- Копирование card Inject из джема  
- AppFlow стейты размазаны по UI-кнопкам без центрального FSM  

---

Связанные: [[02 Scenes & Lifetime]] · [[07 Mentorship & Learning]] · gmtk wiki `Architecture/02`, `04 Game Director`
