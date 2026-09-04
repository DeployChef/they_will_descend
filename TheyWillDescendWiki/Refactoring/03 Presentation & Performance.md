# 03. Оптимизация презентации и памяти

← [[02 Baking, SubScenes & World Bootstrap|02 Бейкинг и саб-сцены]] | [[Index|План рефакторинга]] | Далее → [[04 Step-by-Step Roadmap|04 Дорожная карта]]

Документ описывает устранение утечек производительности на стыке ECS $\rightarrow$ GameObjects и оптимизацию UI.

---

## 1. Проблема: Аллокации NativeArray в `LateUpdate`

В презентационных досках [`AgentViewBoard.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Presentation/Agents/AgentViewBoard.cs#L69-L74) и [`BuildingViewBoard.cs`](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Presentation/City/BuildingViewBoard.cs#L107-L109) синхронизация написана так:

```csharp
// AgentViewBoard.Sync() — КАЖДЫЙ КАДР:
var ids = query.ToComponentDataArray<AgentId>(Allocator.Temp);
var types = query.ToComponentDataArray<AgentType>(Allocator.Temp);
var motors = query.ToComponentDataArray<AgentLocomotion>(Allocator.Temp);
var assignments = query.ToComponentDataArray<AgentAssignment>(Allocator.Temp);
var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
```

### Чем это плохо:
Каждый кадр создается 5 отдельных `NativeArray`. Данные копируются из архетипов памяти в массив, затем утилизируются. Это вызывает постоянную нагрузку на аллокатор памяти (`Temp Allocator`) и микрофризы.

### Целевое решение: Прямой проход без аллокаций
Вместо конвертации всего запроса в массивы используется прямая итерация:

```csharp
// Вариант 1: Через SystemAPI в тонкой Presentation-системе (Zero Allocation):
foreach (var (transform, motor, assignment, id) in 
         SystemAPI.Query<RefRO<LocalTransform>, RefRO<AgentLocomotion>, RefRO<AgentAssignment>, RefRO<AgentId>>())
{
    var agentId = id.ValueRO.Value;
    if (_views.TryGetValue(agentId, out var view))
    {
        view.transform.SetPositionAndRotation(transform.ValueRO.Position, transform.ValueRO.Rotation);
        view.SetMoving(motor.ValueRO.Moving != 0);
    }
}
```
**Результат:** 0 байт аллокаций в кадре, мгновенное копирование позиций.

---

## 2. Замена `DestroyImmediate` на пулинг объектов

В коде очистки видов сейчас написано:
```csharp
go.SetActive(false);
Object.DestroyImmediate(go); // ОПАСНО в Play Mode!
```
Вызов `DestroyImmediate` в рантайме Unity нарушает кадровую синхронизацию рендера и ломает жизненный цикл объектов.

### Решение:
1. Для редких удалений (снос дома) использовать стандартный `Object.Destroy(go)`.
2. Для жителей (спавн, гибель, переход в здания) внедрить простой **`GameObjectPool`**:
   * Когда житель заходит в здание — не уничтожать GameObject, а отправлять в пул (`SetActive(false)`).
   * Когда житель выходит — доставать из пула (`SetActive(true)`).

---

## 3. Оптимизация UI-инспекторов ([BuildingInspectPanel](file:///f:/Unity/they_will_descend/TheyWillDescend/Assets/_Project/Scripts/Presentation/GameHud/BuildingInspectPanel.cs))

### Что исправить:
1. **Кэшировать выбранную `Entity`:** Инспектор должен хранить `Entity _selectedEntity`. Все свойства (`Workplace`, `BuildingType`, `Construction`) считываются за $O(1)$ через `em.GetComponentData<T>(_selectedEntity)` без переборов.
2. **Устранить строковый мусор:** Тексты `workers.text = $"{occupied} / {slots}"` форматировать только тогда, когда значения **действительно изменились** (Dirty flag), а не каждый кадр в `Update()`.
3. **Убрать динамическую сборку UI из кода:** В `PyramidInspectPanel` элементы слайдеров сейчас создаются через `new GameObject(...)` и удаляются через `Destroy`. Их нужно один раз сверстать в префабе или использовать префаб-строку для пула.
