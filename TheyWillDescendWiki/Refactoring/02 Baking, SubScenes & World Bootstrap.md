# 02. Отказ от саб-сцены и бейкинга каталогов

← [[01 ECS & Commands|01 Реформа ECS]] | [[Index|План рефакторинга]] | Далее → [[03 Presentation & Performance|03 Презентация]]

Гайд по ликвидации самой запутанной и хрупкой части текущего проекта: использования **SubScene** и **Baking** для статичных данных и каталогов.

---

## 1. Зачем Unity вообще создала SubScenes и Baking?

Механизм **SubScene + Baker** в Unity DOTS создавался под конкретную задачу:
> **Конвертировать гигантские 3D-локации** (Megacity, открытые миры с 200 000 камней, деревьев, зданий и физических коллайдеров) из тяжелых GameObjects сцены в бинарные чанки сущностей на диске и быстро стримить их в память.

### Что запекается в SubScene в нашем проекте сейчас?
В сцене `Game.unity` в саб-сцене `Simulation` лежат:
* `BuildingCatalogAuthoring` (список цен и рецептов зданий)
* `ResourceCatalogAuthoring` (названия и иконки ресурсов)
* `TimelineCatalogAuthoring` (эры и дань)
* `SimControlAuthoring` (синглтон времени)
* `CityGridAuthoring` (параметры полярной сетки)
* `HeadquarterAuthoring` (префаб пирамиды)

**Все динамические объекты игры (дома, которые строит игрок, и все жители) в саб-сцену НЕ входят — они спавнятся кодом!**

Иными словами: **тяжеловесный стриминговый конвейер SubScene используется просто для того, чтобы перегнать несколько ScriptableObject-таблиц с числами в DynamicBuffer!** Это архитектурный нонсенс.

---

## 2. Почему текущий бейкинг — это мучение и источник багов

1. **Асинхронный лаг и 30-секундный таймаут:**
   При входе в Play Mode или загрузке уровня Unity асинхронно компилирует/стримит саб-сцену. Из-за этого в [`GameSession.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Presentation/Shell/GameSession.cs#L35) пришлось прописать:
   ```csharp
   [SerializeField] float simulationReadyTimeoutSeconds = 30f;
   ```
   Игра не может начаться, пока цикл `WaitUntilSimulationReady` не дождется появления забейканных буферов.
2. **Фантомные баги кэша Unity:**
   Ты меняешь стоимость лесопилки в ScriptableObject $\rightarrow$ запускаешь Play Mode $\rightarrow$ цена осталась старой! Потому что генератор кэша бейкинга Unity не понял, что зависимость изменилась, и не обновил саб-сцену. Приходится делать Reimport.
3. **Четверной бойлерплейт на каждый чих:**
   Чтобы добавить один параметр ресурса, нужно:
   `ScriptableObject` $\rightarrow$ `Authoring MonoBehaviour` $\rightarrow$ `Baker<TAuthoring>` $\rightarrow$ `IComponentData`. Четыре класса ради одного поля!
4. **Кошмар при перезапуске рана (Restart):**
   Поскольку сущности каталогов забейканы саб-сценой, ты не можешь при рестарте просто сказать: `EntityManager.DestroyEntity(UniversalQuery)`. Иначе умрут каталоги и игра сломается!
   Именно из-за этого родился костыльный метод `RunPublisher.BeginReset()`, который вручную отправляет команды `DespawnAllAgentsCommand` и `DespawnAllBuildingsCommand`, аккуратно обходя сущности саб-сцены.

---

## 3. Целевое решение: Чистый Runtime C# Bootstrap

Вместо саб-сцены инициализация мира делается **одним синхронным методом на старте рана**.

### Как это устроено:

```text
GameSession.StartAsync
       │
       ▼
SimulationBootstrap.InitializeRun(em, scenarioSO, difficultySO, catalogsSO)
       │
       ├─ 1. Создает сессионную Entity
       ├─ 2. Копирует каталоги из ScriptableObjects в DynamicBuffer (0.001 сек)
       ├─ 3. Инициализирует полярную сетку CityGrid и время GameTime
       └─ 4. Спавнит Пирамиду (HQ) по данным сценария
       │
       ▼
Симуляция готова МГНОВЕННО (0 мс задержки, без таймаутов и ожидания SubScene)
```

### Пример чистого Bootstrap-кода:

```csharp
namespace TheyWillDescend.Simulation.Session
{
    public static class SimulationBootstrap
    {
        public static Entity InitializeRun(
            EntityManager em,
            ScenarioDefinition scenario,
            DifficultyProfile difficulty,
            BuildingCatalogAsset buildingCatalog,
            ResourceCatalogAsset resourceCatalog)
        {
            // 1. Создаем синглтон сессии
            var session = em.CreateEntity();
            em.AddComponentData(session, new SimSession { Phase = SimSessionPhase.Ready });
            em.AddComponentData(session, new SimControl { Speed = 1f, Mode = SimRunMode.Running });
            em.AddComponentData(session, new GameTime { DayDuration = 60f });

            // 2. Напрямую наполняем каталоги из ScriptableObjects (быстро и надежно!)
            PopulateBuildingCatalog(em, session, buildingCatalog, difficulty);
            PopulateResourceCatalog(em, session, resourceCatalog);

            // 3. Создаем Пирамиду (HQ)
            SpawnHeadquarters(em, session, scenario);

            return session;
        }

        static void PopulateBuildingCatalog(
            EntityManager em, 
            Entity session, 
            BuildingCatalogAsset catalog, 
            DifficultyProfile difficulty)
        {
            var prototypes = em.AddBuffer<BuildingPrototype>(session);
            var costs = em.AddBuffer<BuildingCatalogCost>(session);
            var recipes = em.AddBuffer<BuildingCatalogRecipe>(session);

            foreach (var item in catalog.Buildings)
            {
                // Прямой перенос данных без всяких Baker'ов
                prototypes.Add(new BuildingPrototype { ... });
                // Применяем оверрайды сложности на лету
            }
        }
    }
}
```

---

## 4. Что это дает проекту?

| Проблема саб-сцены | Решение через C# Bootstrap |
| :--- | :--- |
| Ожидание бейкинга до 30 секунд при старте | **Мгновенный старт за 0.001 секунды** |
| Непредсказуемый кэш Unity (Reimport) | **100% актуальные данные из SO при каждом старте** |
| Гора файлов Authoring и Baker | **Папка Authoring сокращается на 70%** |
| Сложный рестарт через Despawn-команды | **Рестарт за 1 строчку:** очистить мир и позвать `InitializeRun` |
| Суб-сцена в `Game.unity` | **Чистая сцена `Game.unity` без служебных контейнеров** |

---

## 5. Нужна ли вообще SubScene где-либо в They Will Descend?

* **Для каталогов, настроек, сценариев:** **НЕТ!** (Только ScriptableObjects + прямой Bootstrap).
* **Для декораций уровня (земля, окружение):** Если на карте появятся тысячи статичных камней и деревьев, которые нужно рендерить без GameObject — их можно вынести в отдельную декоративную саб-сцену `Environment_SubScene`.
* Но **логика, экономика и каталоги больше никогда не должны запекаться через саб-сцену**.
