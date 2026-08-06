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

### Конфиг (канон сейчас)

```text
RadialGridConfig
  InnerRadius, RadialStep (низкие кольца), RingCount (много)
  InnerBandClusterCount = 66   // кольца 0–1: 11 домов × ширина 6

TargetClusterWorldWidth = 2π * RingMid(0) / 66

GetClusterCount(ring):
  rings 0–1 → 66
  дальше → round(2π * RingMid(bandStart) / TargetClusterWorldWidth)
  (парами колец: 0–1, 2–3, …)
```

**Нет fine/micro-grid.** Подложка кластеров — правило для зданий. Дороги позже почти свободны по углу.  
Размер дома в мире ≈ `WidthClusters * TargetClusterWorldWidth` на любом кольце.

### Сущности

| Что | Как |
| --- | --- |
| Здание | footprint кластеров якоря × кольца; occupy по дуге якоря |
| Дорога | позже свободнее, не micro-snap |
| Blocked | флаги на кластерах / зонах |

## 5. Слои

| Слой | Роль |
| --- | --- |
| Presentation | CityCenter, underlay, ghost |
| Simulation | config, occupancy (позже) |
| Shell | режимы стройки |

## 7–8. Статус

- Underlay + place stub (House 6×2 / Cube 2×2) работают.
- Fine-разметки нет (осознанно, по FP).
- Between-ring placement / rotate / ECS occupy — дальше.

---

Связанные: [[04 Simulation]] · [[08 Production ECS]] · [[10 Vertical Slice — Shell + ECS Walkers]] · [[11 Camera & Presentation Scenes]]
