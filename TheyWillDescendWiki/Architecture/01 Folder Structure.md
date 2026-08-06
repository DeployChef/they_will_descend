# 01 Folder Structure

← [[00 Overview]] | [[Index]] | Далее → [[02 Scenes & Lifetime]]

## Цель

Чистый `Assets/_Project` с предсказуемыми зонами. Shell ≠ Simulation.

## Целевая структура

```
Assets/_Project/
  Art/
  Audio/
  Prefabs/
  Scenes/                 # Boot/Root, MainMenu, Game (Bootstrap пока = Game-зародыш)
  SubScenes/              # Simulation и др. для bake
  Scripts/
    Infrastructure/       # Logging (GameLog), утилиты
    Shell/                # AppFlow, Director, SimGate, Root/Game scopes (когда появятся)
    Simulation/           # ECS: Time, Economy, … (IComponentData, ISystem)
    Authoring/            # MonoBehaviour + Baker
    Presentation/         # UI, камера, bridges ECS→UI
    Content/              # каталоги сценариев, defs (по мере роста)
  Settings/               # balance SO, project settings assets
```

## Сейчас (факт)

```
Assets/_Project/
  Art/
  Scenes/Bootstrap.unity
  SubScenes/Simulation.unity
  Scripts/
    Authoring/Time/
    Simulation/Time/
    Infrastructure/Logging/
    Presentation/          # пусто, зарезервировано
```

Asmdefs появятся при выносе Shell/VContainer (границы как в gmtk: Core / Shell / Simulation / Presentation), без Inject симуляции.

---

Связанные: [[00 Overview]] · [[09 App Shell]] · [[05 Content Pipeline]]
