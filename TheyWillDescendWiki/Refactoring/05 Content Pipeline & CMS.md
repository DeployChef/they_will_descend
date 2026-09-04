# 05. Реформа контент-пайплайна и CMS (Data-Driven Архитектура)

← [[04 Step-by-Step Roadmap|04 Дорожная карта]] | [[Index|План рефакторинга]] | [[../Home|Главная вики]]

Гайд по переходу от жестко зашитых в префабы характеристик к чистой, гибкой **Data-Driven CMS** (Single Source of Truth), разделяющей экономические данные и 3D-визуал.

---

## 1. Проблема текущей реализации: «Баланс зашит в префабы»

В текущей архитектуре ([`05 Content Pipeline.md`](file:///f:/Unity/they_will_descend/TheyWillDescendWiki/Architecture/05%20Content%20Pipeline.md)) источником правды для зданий объявлен **префаб**:
* На префабе `Kitchen.prefab` и `Sawmill.prefab` висит монобех [`BuildingStamp.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Content/BuildingStamp.cs).
* В префаб зашиты: `footprint`, стоимость постройки (`costs`), слоты рабочих (`workplaceSlots`), рецепт производства (`recipeInputs`, `recipeOutputs`).

### Почему это ломает разработку:
1. **Префаб — это View (Визуал), а не Data (Данные):**  
   Префаб должен отвечать за меш, материалы, анимации Mixamo, партиклы дыма и звуковые точки. Зашивать внутрь префаба экономические формулы — грубая архитектурная ошибка.
2. **Слепота геймдизайнера:**  
   Чтобы оценить баланс экономики, ГД вынужден поочередно открывать 20 префабов в Inspector. Невозможно увидеть всю экономику в одном окне, сравнить отдачу ресурсов на одного рабочего и построить графики окупаемости.
3. **Git-конфликты в бинарных `.prefab`:**  
   Художник подвинул модель кухни, а геймдизайнер в это же время изменил стоимость стройки на 5 дерева. Итог — конфликт слияния в Unity-префабе, который почти невозможно разрешить вручную.
4. **Костыльная система сложностей ([`DifficultyProfile.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Content/DifficultyProfile.cs)):**  
   Поскольку данные зашиты в префаб, для изменения баланса под разные сложности пришлось придумать оверлей с булевыми флагами:
   ```csharp
   public struct DifficultyBuildingOverride {
       public string typeId;
       public bool replaceConstruction;
       public bool replaceSlots;
       public bool replaceCosts;
       public bool replaceRecipe;
   }
   ```
   В результате одни цифры лежат на префабе, другие — в оверлее. Баланс размазан, концов не найти.

---

## 2. Фундаментальный принцип: «Данные владеют визуалом»

В профессиональной архитектуре стрелка зависимости разворачивается:
> **Не префаб хранит данные, а Данные ссылаются на Префаб.**

```text
               BuildingDefinition (ScriptableObject / CMS)
          ┌───────────────────────────┴───────────────────────────┐
          ▼                                                       ▼
   ЭКОНОМИКА И БАЛАНС                                    ВИЗУАЛ И ПРЕЗЕНТАЦИЯ
  (Чистые числа и правила)                               (Ссылка на префаб)
  - TypeId: "sawmill"                                    - VisualPrefab: Sawmill.prefab
  - Footprint: 6x2                                         (только Mesh, Animator,
  - Cost: 15 Wood                                           BuildingView, точки дыма)
  - Slots: 5
  - Recipe: -4 Wood/h -> +10 Planks/h
```

**Префаб становится абсолютно «чистым»:** на нем нет никаких цен, рецептов и слотов. Его можно менять, перекрашивать, анимировать — баланс игры от этого не дрогнет.

---

## 3. Модель данных целевой CMS

### 3.1. Паспорт здания (`BuildingDefinition.cs`)
Чистый ScriptableObject, лежащий в `Assets/_Project/Database/Buildings/`:

```csharp
namespace TheyWillDescend.Database
{
    [CreateAssetMenu(fileName = "Building_", menuName = "They Will Descend/Database/Building Definition")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string TypeId = "sawmill";
        public string DisplayName = "Лесопилка";
        public Sprite Icon;
        
        [Header("Footprint")]
        public int WidthClusters = 6;
        public int DepthRings = 2;

        [Header("Construction")]
        public float ConstructionDuration = 10f;
        public int ConstructionCrewSlots = 3;
        public ResourceCost[] Costs;

        [Header("Workforce & Production")]
        public int WorkerSlots = 5;
        public bool IsResearchWorkplace;
        public ResourceRate[] Inputs;
        public ResourceRate[] Outputs;

        [Header("Presentation")]
        public GameObject VisualPrefab; // Голый префаб (Mesh + Animator + BuildingView)
    }
}
```

### 3.2. Каталог ресурсов (`ResourceDefinition.cs`)
Лежит в `Assets/_Project/Database/Resources/`:
```csharp
namespace TheyWillDescend.Database
{
    [CreateAssetMenu(fileName = "Resource_", menuName = "They Will Descend/Database/Resource Definition")]
    public sealed class ResourceDefinition : ScriptableObject
    {
        public string ResourceId = "wood";
        public string DisplayName = "Древесина";
        public Sprite Icon;
        public float EnergyValue = 1f; // Ценность для пламени Пирамиды
        public float BaseStockCap = 500f;
        public bool CanFeedPyramid = true;
    }
}
```

### 3.3. Единая база данных игры (`GameDatabase.cs`)
Единый реестр всего контента проекта:
```csharp
namespace TheyWillDescend.Database
{
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "They Will Descend/Database/Game Database")]
    public sealed class GameDatabase : ScriptableObject
    {
        [Header("Catalogs")]
        public List<BuildingDefinition> Buildings = new();
        public List<ResourceDefinition> Resources = new();
        
        [Header("Rules")]
        public GameRulesDefinition Rules; // Длина суток, смена, скорость ходоков
    }
}
```

---

## 4. Честная система сложностей (`DifficultyProfile.cs`)

Вместо дублирования рецептов со свитчами `replaceRecipe`, сложность задается **системными мультипликаторами и стартовыми условиями**:

```csharp
namespace TheyWillDescend.Database
{
    [CreateAssetMenu(fileName = "Difficulty_", menuName = "They Will Descend/Database/Difficulty Profile")]
    public sealed class DifficultyProfile : ScriptableObject
    {
        public string Name = "Испытание Богов (Hard)";

        [Header("Мультипликаторы экономики")]
        [Tooltip("Коэффициент стоимости стройки (1.25 = на 25% дороже)")]
        public float BuildingCostMultiplier = 1.25f;

        [Tooltip("Коэффициент расхода ресурсов Пирамидой")]
        public float PyramidDrainMultiplier = 1.3f;

        [Tooltip("Скорость угасания лояльности богов")]
        public float LoyaltyDecayMultiplier = 1.4f;

        [Header("Стартовые ресурсы")]
        public int StartingWorkers = 6;
        public ResourceStockEntry[] StartingResources;
    }
}
```

Если на определенной сложности здание вообще запрещено строить — в профиль добавляется список `List<string> LockedBuildingTypeIds`.

---

## 5. Табличный подход (Google Sheets / CSV Sync)

Для профессиональной балансировки экономики баланс выносится в Google Таблицы:

### Структура таблицы Google Sheets:
| TypeId | DisplayName | Width | Depth | CostWood | CostStone | BuildTime | Slots | InResource | InRate | OutResource | OutRate | PrefabPath |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `sawmill` | Лесопилка | 6 | 2 | 15 | 0 | 10 | 5 | — | 0 | `wood` | 12 | `Prefabs/Sawmill` |
| `kitchen` | Кухня | 2 | 2 | 8 | 0 | 6 | 3 | `wood` | 6 | `food` | 12 | `Prefabs/Kitchen` |

### Импортер в Unity:
В Unity пишется редакторская утилита `Tools > Sync Balance from Google Sheets`:
1. Скачивает публичный CSV-экспорт таблицы по URL за 0.5 секунды.
2. Парсит строки и автоматически создает/обновляет ассеты `BuildingDefinition` в проекте.
3. **Результат:** Геймдизайнер крутит формулы в Google Sheets, жмет одну кнопку в Unity — и весь баланс игры обновлен без единого коммита в префабы.

---

## 6. Как контент попадает в симуляцию ECS

На старте рана метод `SimulationBootstrap.InitializeRun` за **0.001 секунды** без всякого бейкинга переносит данные из `GameDatabase` в структуры ECS:

```csharp
public static void PopulateCatalogs(
    EntityManager em, 
    Entity session, 
    GameDatabase db, 
    DifficultyProfile difficulty)
{
    var prototypes = em.AddBuffer<BuildingPrototype>(session);
    var costs = em.AddBuffer<BuildingCatalogCost>(session);
    var recipes = em.AddBuffer<BuildingCatalogRecipe>(session);

    var costMult = difficulty != null ? difficulty.BuildingCostMultiplier : 1.0f;

    foreach (var b in db.Buildings)
    {
        // 1. Создаем прототип
        prototypes.Add(new BuildingPrototype
        {
            TypeId = b.TypeId,
            WidthClusters = b.WidthClusters,
            DepthRadialRings = b.DepthRings,
            ConstructionDuration = b.ConstructionDuration,
            WorkplaceSlots = b.WorkerSlots
        });

        // 2. Рассчитываем итоговую стоимость с учетом сложности
        foreach (var cost in b.Costs)
        {
            costs.Add(new BuildingCatalogCost
            {
                TypeId = b.TypeId,
                ResourceId = cost.Resource.ResourceId,
                Amount = (int)(cost.Amount * costMult)
            });
        }

        // 3. Добавляем рецепт производства
        // ...
    }
}
```

---

## 7. Пошаговый план миграции (Migration Plan)

### Этап 1. Создание новых структур данных
1. Создать классы: `BuildingDefinition.cs`, `GameDatabase.cs`, `DifficultyProfile.cs` (новая версия).
2. Создать папку `Assets/_Project/Database/Buildings/`.

### Этап 2. Конвертация существующих зданий
1. Написать разовый Editor-скрипт `MigrateBuildingStampsToDefinitions`:
   * Находит все префабы с `BuildingStamp` (`Kitchen.prefab`, `Sawmill.prefab`).
   * Создает ассет `BuildingDefinition` для каждого здания.
   * Копирует все поля (`typeId`, `footprint`, `costs`, `recipes`, `slots`).
   * Прикрепляет префаб в поле `VisualPrefab`.
2. Снять компонент `BuildingStamp` со всех префабов.

### Этап 3. Переключение систем
1. В `GameDatabase.asset` собрать список всех созданных `BuildingDefinition`.
2. Переписать `SimulationBootstrap` на чтение `GameDatabase` вместо старого `BuildingCatalogAuthoring`.
3. В `BuildPlacementController` и `BuildingViewBoard`: брать иконки и префабы напрямую из `GameDatabase.Buildings`.

### Этап 4. Удаление старого бойлерплейта
1. Удалить `BuildingStamp.cs`.
2. Удалить старый `DifficultyBuildingOverride`.
3. Удалить `BuildingCatalogAuthoring.cs` и `BuildingStampRead.cs`.

---

## Итог реформы CMS

| Аспект | До рефакторинга | После рефакторинга |
| :--- | :--- | :--- |
| **Где живет баланс** | Зашит в префабы (`BuildingStamp.cs`) | В отдельных ScriptableObjects (`BuildingDefinition`) |
| **Роль префаба** | База данных + Визуал | Исключительно визуал (Mesh, Animator, VFX) |
| **Сложность** | Костыльный оверлей с булевыми флагами | Прозрачные системные мультипликаторы |
| **Git-конфликты** | Частые в `.prefab` | Исключены (данные в `.asset`, визуал в `.prefab`) |
| **Google Sheets** | Невозможно | Синхронизация в 1 клик через CSV |
| **Загрузка в ECS** | Медленный бейкинг через SubScene | Мгновенный перенос за 0.001 секунды в C# Bootstrap |
