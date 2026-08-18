# 02 Scenes & Lifetime

← [[01 Folder Structure]] | [[Index]] | Далее → [[03 Core Systems]]

Полный контракт оболочки: [[09 App Shell]]. Камеры: [[11 Camera & Presentation Scenes]].

## Сцены (канон)

| Сцена | Живёт | Содержимое |
| --- | --- | --- |
| **Bootstrap** (Root) | всегда | `Startup`, **одна** Main Camera (+ Cinemachine Brain позже), EventSystem. Без мира, без меню-UI, без gameplay-света |
| **MainMenu** | пока в меню | Canvas splash/menu, `ShellUiBinder`, опционально VCam/меню-свет |
| **Game** | сессия рана | уровень, Directional Light, VCam геймплея, SubScene Simulation. **Без** второй Main Camera |

Поток:

```text
Bootstrap (вечный)
  → load MainMenu (additive)   // Press Any Key → кнопки
  → «Начать» → unload MainMenu (или hide), load Game
  → Playing / Paused
  → выход в меню → unload Game, load MainMenu
```

Меню **не** содержит игровой мир. Game появляется только по Start.

## Lifetime

Сейчас без VContainer: `Startup` + `GameSession` / `SceneLoader`.  
Позже Root/Game scopes — только Shell/Presentation, не ECS.

## SimGate

Off на меню/загрузке · Running в Playing · Frozen на паузе. Speed (x1/x2/x3) рядом с Mode.  
[[09 App Shell]] · тулза и save: [[13 Time HUD and Save]].

---

Связанные: [[11 Camera & Presentation Scenes]] · [[09 App Shell]] · [[10 Vertical Slice — Shell + ECS Walkers]]
