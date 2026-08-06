# 02 Scenes & Lifetime

← [[01 Folder Structure]] | [[Index]] | Далее → [[03 Core Systems]]

Полный контракт оболочки: [[09 App Shell]]. Здесь — сцены и lifetime кратко.

## Сцены (целевые)

| Сцена | Роль |
| --- | --- |
| `Boot` / `Root` | вечная: Startup, DI root, FMOD, AppFlow, Director, shell bus |
| `MainMenu` | мета, выбор сценария (рано можно панелями на Root) |
| `Game` | сессия рана: камера, HUD, child scope, **SubScene Simulation** |

### Сейчас в репо (учебный срез)

| Сцена | Роль |
| --- | --- |
| `Assets/_Project/Scenes/Bootstrap.unity` | зародыш Game: камера, свет, SubScene |
| `Assets/_Project/SubScenes/Simulation.unity` | authoring → bake в ECS |

Bootstrap позже станет `Game` или будет загружаться из Root.

## Lifetime / DI

Схема джема **сохраняется для Shell**, не для симуляции:

```
Root (вечная)
  RootLifetimeScope  → AppFlow, Director, SimGate, Audio, Shell bus
Game (additive)
  GameLifetimeScope (child) → session presentation, bridges
  SubScene Simulation → ECS world (без VContainer)
```

- Auto Run у scopes **выключен**; `Build()` после load и привязки parent (как в gmtk).
- Game scope Dispose при выгрузке рана.

## SimGate

Симуляция тикает только когда Shell ставит **Running**.  
Меню / брифинг / катсцена → **Off**. Пауза → **Frozen**.  
Детали: [[09 App Shell]].

## Pause

Presentation pause (оверлеи) может использовать ref-counted keys (идея gmtk).  
Экономика/дни — только через SimGate, не через один `timeScale`.

---

Связанные: [[09 App Shell]] · [[03 Core Systems]] · [[08 Production ECS]]
