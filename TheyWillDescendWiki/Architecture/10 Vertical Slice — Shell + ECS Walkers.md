# 10 Vertical Slice — Shell + ECS Walkers

← [[09 App Shell]] | [[Index]]

Целевой процесс **ближайших заходов** (не вся игра). Любой временный костыль в чате помечается явно: *временно / канон / заменим когда*.

## Конечный UX этого среза

```text
1. Press Any Key          (экран-заставка)
2. Main Menu              (одна кнопка «Начать игру»)
3. Playing                (SimGate.Running — челики ходят по карте через ECS)
   ⇄ Paused (Esc)         (SimGate.Frozen — челики стоят)
```

Источник окружения: сейчас `Assets/Scenes/SampleScene.unity` + `NpcCircleWalker` (MonoBehaviour).  
Цель: окружение в `_Project`, ходьба — ECS + уважение к `SimControl`.

## Заходы

| # | Цель | Результат |
| --- | --- | --- |
| **A** | AppFlow: PressAnyKey → MainMenu → Playing → Paused | Кнопка «Начать», дни/sim gate как сейчас |
| **B** | Перенос сцены Sample → `_Project/Scenes/Game` | Камера/свет/уровень/NPC в правильном месте; Startup на Game |
| **C** | Ходьба NPC через ECS | Authoring + system кругового движения; MB walker убрать с этих NPC |
| **D** | Frozen останавливает ходьбу | Move system early-out если не Running (как Time) |

## Как правильно переносить сцену (канон)

**Не** «вырезать куски в Bootstrap наугад».

1. `SampleScene` → Save As → `Assets/_Project/Scenes/Game.unity` (копия, оригинал можно оставить).  
2. На `Game`: добавить то, чего не хватает из shell (`Startup`, UI Canvas меню/press-any-key, ссылка на SubScene Simulation).  
3. Выкинуть мусор демки, если мешает (лишние камеры — одна Main).  
4. **Статика уровня** (дома, земля) может жить обычными GameObjects на `Game`.  
5. **Агенты (челики)** — в **Simulation SubScene** (или отдельной Agents SubScene), чтобы bake → entities.  
6. Build Settings: стартовая сцена = `Game` (пока нет отдельного Boot); позже Boot → load Game.

Почему челики в SubScene: движение — sim; должно подчиняться `SimControl` и baking.

## Временно vs канон (честно)

| Сейчас / скоро | Статус |
| --- | --- |
| `SimGate` + `SimControlSyncSystem` | **канон шва** Shell→ECS |
| Input map Shell в коде | **временно** → потом `.inputactions` asset |
| `SimGate.Active` static | **временно ок** для одного gate; при тестах/мультимире — инжект |
| UI PressAnyKey / Menu на Canvas | **канон** для этого среза |
| Ходьба кругом как у `NpcCircleWalker` | **временно** механика; важно что ECS + SimGate |

## Ссылки на код

- Shell: `Scripts/Shell/`
- Sim control: `Scripts/Simulation/Session/`
- Старый walker: `Assets/Scripts/NpcCircleWalker.cs` (заменим на ECS в заходе C)
- Сцена-источник: `Assets/Scenes/SampleScene.unity`

---

Связанные: [[09 App Shell]] · [[07 Mentorship & Learning]]
