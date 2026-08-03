# 05 Content Pipeline

← [[04 Simulation]] | [[Index]]

## ScriptableObjects

- ResourceDefinition
- BuildingDefinition
- LawDefinition
- EventDefinition / CrisisDefinition
- ScenarioDefinition (кампания / вертикальный срез)

## Баланс

Числа живут в данных (SO / таблицы), не размазаны по MonoBehaviour.  
См. [[../Balance/Balance|Balance]].

## Авторский поток

1. Дизайн пишет правило в GDD
2. Контент-описание → SO / таблица
3. Симуляция читает definition
4. Баланс крутит параметры без переписывания логики

---

Связанные разделы: [[01 Folder Structure]] · [[../GDD/10 Roadmap|Roadmap]]
