# 12 Radial City Grid (Frostpunk-like)

← [[10 Vertical Slice — Shell + ECS Walkers]] | [[Index]]

Канон целевой модели города. **Кода пока нет** — прототип coarse-сетки удалён; следующий чат стартует с этой заметки.

> Наблюдения сверены с Frostpunk в игре (не реверс движка).

## 1. Суть одной фразой

Город живёт в **полярных координатах** вокруг центра (у нас — пирамида / плаза). Снап идёт не к крупным «тортовым» секциям UI, а к **мелкой дискретной сетке** `(angularIndex, radialIndex)`. Здания и дороги занимают **наборы** этих единиц. Крупные лучи/кольца на экране — только **гайд**, не истина симуляции.

## 2. Что видно в Frostpunk (наблюдения)

| Наблюдение | Вывод |
| --- | --- |
| Нет жёсткой «шахматной» сетки клеток как в square city-builder | Модель полярная, не ортогональная |
| Есть окружность и лучи от центра | Visual guide / build helpers |
| «Секции» выглядят разного размера / не жёстко фиксированы | UI-дольки ≠ атомарная единица; packing/footprint важнее |
| Дом на ~6 делений; другой 3×6 с поворотом | Footprint = `W × D` в fine units + rotation |
| Дом может «чуть больше клетки» | Fine units + иногда smart shrink/expand |
| Дорогу от центра можно провести почти под любым углом | Снап к **мелкому** angular step, не к границам крупных секций |
| Дорога может идти по центру секции, по ⅓, «почти по пикселям» | `AngularDivisions` большое (порядок сотен) |

## 3. Неверная модель (что мы уже отвергли)

```text
Город = ringCount × sectorCount фиксированных клеток (типа 8×24)
Всё (дома, дороги) только по границам этих клеток
```

Это удобно для первого урока polar math, но **не** модель Frostpunk и не наш целевой канон.

## 4. Целевая модель

```text
World (x,z)
    →  polar (angle, radius) относительно CityCenter
    →  snap к fine cell (angularIndex, radialIndex)
    →  occupy / cost / road flags на этих cells

Visual guide (опционально):
    редкие spokes (каждый N-й angular)
    редкие rings (каждый K-й radial или зоны тепла)
```

### Конфиг (идея)

```text
RadialGridConfig
  float3 / Transform  Center
  float               InnerRadius      // пустая плаза / запретная зона
  float               RadialStep       // толщина одного radial unit
  int                 AngularDivisions // напр. 256 / 360 / 512
  int                 MaxRadialIndex
```

### Формулы

```text
// world → fine
delta  = pos - Center
radius = length(delta.xz)
angle  = atan2(delta.x, delta.z)   // один раз выбрать convention и не менять

angularIndex = floor(normalizedAngle / (2π) * AngularDivisions)
radialIndex  = floor((radius - InnerRadius) / RadialStep)

// fine → world (центр ячейки)
θ = (angularIndex + 0.5) * (2π / AngularDivisions)
r = InnerRadius + (radialIndex + 0.5) * RadialStep
pos = Center + (sin(θ), 0, cos(θ)) * r
```

### Сущности поверх fine grid

| Что | Как кодируется |
| --- | --- |
| Дорога-spoke | почти постоянный `angularIndex`, диапазон `radialIndex` |
| Дорога-arc | почти постоянный `radialIndex`, диапазон `angularIndex` |
| Здание | footprint `widthAngular × depthRadial` (+ поворот 90° и т.п.), occupy cells |
| Снег / void | walk cost без дороги / вне карты |
| Blocked | cell под зданием |

Pathfinding (позже): A* / flow **по fine cells** (или dual graph только дорог). Крупные секции в pathfinding не участвуют.

## 5. Слои (когда начнём код)

| Слой | Роль |
| --- | --- |
| Presentation | CityCenter transform, optional guide mesh, ghost footprint, hover |
| Simulation (ECS) | config blob/singleton, occupancy / road / cost buffers |
| Shell | режимы стройки (дорога / здание) — позже |

Правило проекта: истина занятости — в симуляции; меш на Game — картинка.

## 6. Smart packing (осознанно позже)

В FP соседние здания того же типа могут чуть сжиматься/растягиваться, чтобы заполнить кольцо.  
**Не делать в первых шагах.** Сначала честный fixed footprint в fine units.

## 7. План заходов (с нуля)

| Шаг | Цель | Код сейчас |
| --- | --- | --- |
| **F0** | `RadialGridConfig` + math `world ↔ (angular, radial)` | нет |
| **F1** | Редкий visual guide + hover fine cell | нет |
| **F2** | Paint road (spoke/arc) со снэпом к fine | нет |
| **F3** | Ghost building N×M + rotate | нет |
| **F4** | Place → occupy fine cells | нет |
| **F5** | Pathfinding по fine + road/snow cost | нет |
| **F6** | Smart packing (опционально) | нет |

`CircleWalk` агентов — отдельная заглушка движения по кругу; к городской сетке не относится. Pathfinding по дорогам его заменит для «иди на работу».

## 8. Статус репо

- Прототип coarse (`RadialCoords`, `RadialGridView`, hover, materials, `CityCenter` / `RadialGrid` на Game) — **удалён**.
- Эта заметка — единственный канон до нового кода.
- Стартовать новый чат с: «делаем F0 по Architecture/12».

---

Связанные: [[04 Simulation]] · [[08 Production ECS]] · [[10 Vertical Slice — Shell + ECS Walkers]] · [[11 Camera & Presentation Scenes]]
