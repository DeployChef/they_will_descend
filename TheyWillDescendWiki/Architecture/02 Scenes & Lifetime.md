# 02 Scenes & Lifetime

← [[01 Folder Structure]] | [[Index]] | Далее → [[03 Core Systems]]

Полный контракт оболочки: [[09 App Shell]]. Камеры: [[11 Camera & Presentation Scenes]].

## Сцены (канон)

| Сцена | Живёт | Содержимое |
| --- | --- | --- |
| **Bootstrap** | всегда | хосты: Main Camera + AudioListener, EventSystem, `Startup`, `GameAudio`, `GameInput`, `GameSession`. Без мира, без меню-canvas, без gameplay-света |
| **MainMenu** | пока в меню | Canvas: `PressAnyKeyScreen`, `MainMenuScreen`. Опционально VCam/меню-свет. Без Main Camera |
| **Loading** | переход в ран | экран загрузки, пока грузится Game |
| **Game** | сессия рана | уровень, Directional Light, VCam геймплея, HUD, SubScene Simulation. **Без** второй Main Camera |

Поток:

```text
Bootstrap (вечный)
  → load MainMenu (additive)     // Press Any Key → кнопки
  → «Начать» → Loading + Game, unload MainMenu
  → Playing (пауза часов = SimControl, не другой app-state)
  → выход в меню → unload Game, load MainMenu
```

`skipMenuToGameTemporarily` на `Startup` — **отладочный флаг, по умолчанию выключен**. Не канон потока.

Build list: Bootstrap (0), MainMenu, **Loading**, Game.

Меню **не** содержит игровой мир. Game появляется только по Start.

## Lifetime

Сейчас без VContainer: `Startup` + `GameSession` / `SceneLoader` (UniTask).  
Позже Root/Game scopes — только Shell/Presentation, не ECS.

Хосты Bootstrap — **соседи**, ссылки в инспекторе. Не `GetComponent` / `AddComponent` между ними.

## Часы рана (не отдельный стейт)

Off, пока нет сессии · Running в Playing · Frozen, если `PlayerPaused` или `BuildLocked` (стейт остаётся Playing). Speed (x1/x2/x3) на том же `SimControl`.  
Истина — ECS. UI пишет `SimClockCommand`. [[09 App Shell]] · тулза и save: [[13 Time HUD and Save]].

---

Связанные: [[11 Camera & Presentation Scenes]] · [[09 App Shell]] · [[10 Vertical Slice — Shell + ECS Walkers]]
