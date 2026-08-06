# 04 Simulation

← [[03 Core Systems]] | [[Index]] | Далее → [[05 Content Pipeline]]

## Домены

| Домен | Ответственность |
| --- | --- |
| City / Buildings | слоты, постройки, производство |
| People | нужды, труд, мораль, смертность |
| Economy | ресурсы, склады, цепочки |
| Survival pressure | тепло/жизнь / защита от катаклизмов |
| Gods | дань, гнев, фазы |
| Laws | активные модификаторы и триггеры |

## Правило

Симуляция тикает **только при SimGate.Running** (Shell), не от кнопок меню.  
Внутри ECS: Time → Commands → домены → Events.  
Presentation отображает и шлёт Intent/Commands вниз.  
Оболочка: [[09 App Shell]].

---

Связанные разделы: [[../GDD/03 City & People|City & People]] · [[../GDD/04 Economy & Heat|Economy & Heat]]
