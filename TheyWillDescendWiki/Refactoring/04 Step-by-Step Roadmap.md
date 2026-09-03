# 04. Пошаговая дорожная карта рефакторинга

← [[03 Presentation & Performance|03 Презентация]] | [[Index|План рефакторинга]]

Практический план перехода к целевой архитектуре. Рефакторинг разбит на изолированные шаги так, чтобы игра оставалась работоспособной после каждого этапа.

---

## Шаг 1. Runtime Bootstrap вместо SubScene (Быстрая победа)

**Цель:** Избавиться от асинхронного ожидания, таймаутов и кэша бейкинга при старте.

1. **Создать `SimulationBootstrap.cs`:**
   * Статический метод `InitializeRun(...)`, принимающий `EntityManager`, `ScenarioDefinition`, `BuildingCatalogAsset`, `ResourceCatalogAsset`.
   * Создает сессионную сущность, наполняет каталоги зданий, ресурсов и параметры времени напрямую из ScriptableObjects.
2. **Переключить `GameSession.cs`:**
   * Заменить ожидание появления саб-сцены (`WaitUntilSimulationReady`) на вызов `SimulationBootstrap.InitializeRun`.
   * Удалить поле `simulationReadyTimeoutSeconds`.
3. **Очистить сцену `Game.unity`:**
   * Отключить и удалить SubScene `Simulation`.
4. **Критерий успеха:** При нажатии Play игра стартует мгновенно (0 мс задержки), каталоги заполнены, здания строятся.

---

## Шаг 2. Рабочие через Desired State (Вариант А)

**Цель:** Ликвидировать команды назначения, линейные поиски и баги при смертях.

1. **Обновить компонент `Workplace`:**
   * Добавить поля: `public int DesiredWorkers`, `public bool IsPaused`.
2. **Написать `WorkforceDispatchSystem`:**
   * Чистая Burst-система, выравнивающая `AssignedCount` к `DesiredWorkers`.
3. **Обновить `BuildingInspectPanel.cs`:**
   * Кнопки `+` и `-` меняют поле `DesiredWorkers` на выбранной `Entity`.
   * Кнопка питания переключает `IsPaused`.
4. **Удалить старый код:**
   * Удалить `AssignWorkerCommand.cs`, `UnassignWorkerCommand.cs`, `SetWorkplacePausedCommand.cs`.
   * Удалить `ConsumeAssignWorkerCommandsSystem.cs`, `ConsumeUnassignWorkerCommandsSystem.cs`, `ConsumeSetWorkplacePausedCommandsSystem.cs`.
   * Удалить соответствующие буферы на `SimSession`.
5. **Критерий успеха:** Рабочие назначаются и снимаются плавно, без фризов и задержек; при включении паузы рабочие расходятся, при снятии — возвращаются.

---

## Шаг 3. Стройка и снос через Request Entities (Вариант Б)

**Цель:** Перевести создание и удаление объектов на каноничный DOTS-паттерн через `EntityCommandBuffer`.

1. **Создать компоненты-запросы:**
   * `PlaceBuildingRequest { TypeId, AnchorCluster, AnchorRadial, ... }`
   * `DemolishBuildingRequest { TargetBuilding }`
2. **Обновить контроллеры ввода:**
   * `BuildPlacementController` при клике создает `PlaceBuildingRequest` через ECB.
   * Кнопка сноса создает `DemolishBuildingRequest` через ECB.
3. **Создать системы обработки:**
   * `ProcessPlaceBuildingRequestsSystem` (проверяет ресурсы, ставит здание, уничтожает запрос).
   * `ProcessDemolishBuildingRequestsSystem` (возвращает ресурсы, сносит здание, уничтожает запрос).
4. **Удалить старый код:**
   * Удалить `PlaceBuildingCommand`, `DeconstructBuildingCommand` и их консьюмеры.
5. **Критерий успеха:** Стройка и снос работают без единого DynamicBuffer на сессии.

---

## Шаг 4. Очистка `SimSession` и оптимизация памяти

**Цель:** Довести кадр до 0 байт GC Allocations и избавиться от `DestroyImmediate`.

1. **Очистить `SimSessionAccess`:**
   * Удалить из синглтона все упраздненные буферы команд.
2. **Оптимизировать `AgentViewBoard` и `BuildingViewBoard`:**
   * Заменить `ToComponentDataArray` на прямую итерацию сущностей без аллокаций.
   * Заменить `DestroyImmediate` на безопасный `Destroy` / пулинг.
3. **Оптимизировать UI-инспекторы:**
   * Кэшировать выбранную `Entity`.
   * Прекратить форматирование неизменившихся строк в `Update()`.
4. **Критерий успеха:** В Unity Profiler график `GC.Alloc` в процессе игры стабильно равен 0 B.

---

## Шаг 5. Реформа CMS: Вынос баланса из префабов в `BuildingDefinition`

**Цель:** Разделить Data и View, сделать префабы чистыми визуальными контейнерами, убрать оверлеи сложности.

1. **Создать `BuildingDefinition.cs` и `GameDatabase.cs`:**
   * ScriptableObject для каждого типа здания с полями: `TypeId`, `DisplayName`, `Icon`, `Footprint`, `Costs`, `WorkerSlots`, `Inputs`, `Outputs`, `VisualPrefab`.
2. **Перенести данные из `BuildingStamp` в `.asset`:**
   * Сконвертировать данные `Kitchen.prefab` и `Sawmill.prefab` в ассеты `BuildingDefinition`.
   * Удалить компонент `BuildingStamp` с префабов (префабы остаются чистым визуалом).
3. **Обновить `SimulationBootstrap`:**
   * Заполнять каталоги ECS напрямую из `GameDatabase` с применением мультипликаторов сложности (`DifficultyProfile.BuildingCostMultiplier`).
4. **Удалить старые классы:**
   * Удалить `BuildingStamp.cs`, `DifficultyBuildingOverride`, `BuildingStampRead.cs`.
5. **Критерий успеха:** Баланс зданий настраивается в карточках `BuildingDefinition` в одном окне, префабы содержат только арт, сложность накладывается математически без оверлеев.

