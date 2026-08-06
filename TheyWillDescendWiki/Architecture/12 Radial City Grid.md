# 12 Radial City Grid (Frostpunk-like)

← [[10 Vertical Slice — Shell + ECS Walkers]] | [[Index]]

Канон полярной застройки. Наблюдения сверены с Frostpunk в игре (не реверс движка).

## 1. Суть одной фразой

Город — **полярная подложка кластеров** вокруг центра (пирамида / плаза).  
Здания занимают `W × D` кластеров; ширина кластера в мире ≈ константа на любом кольце.  
**Микро-fine сетки нет** — дороги в FP почти свободны по углу; у нас здания сидят на кластерах подложки.

## 2. Наблюдения Frostpunk (зафиксировано)

### 2.1 Подложка и размер

| Наблюдение | Вывод |
| --- | --- |
| Нет попиксельной / сверхтонкой angular-сетки | Нет fine-grid как истины; подложка = правило для зданий |
| Дорогу можно вести почти куда угодно | Дороги позже — свободнее зданий |
| Кольца уходят за камеру | `RingCount` большой; не «ровно 10 навсегда» |
| Высота кольца небольшая | `RadialStep` относительно низкий |
| На 1–2 кольце влезает **11 домов шириной 6** | `InnerBandClusterCount = 66` (= 11 × 6) |
| «Шесть» = ширина по дуге | Footprint `6×2` = 6 кластеров × 2 кольца |
| Дальше от центра секций больше | `GetClusterCount(ring)` растёт с радиусом |
| Дома не раздуваются по ширине | Эталон: `TargetClusterWorldWidth` с кольца 0 |
| Кольца 1–2 с одной нарезкой | Пары колец (0–1, 2–3, …) делят `clusterCount` |
| Мышь над центром / «реактором» | Snap на кольцо 0 по углу курсора (build plane) |

### 2.2 Footprint span и «зубцы»

| Наблюдение | Вывод |
| --- | --- |
| Якорь на кольце 2, глубина 2 → на кольце 3 больше секций под той же дугой | Дуга якоря фиксирована; внешнее кольцо режет её мельче (2 → 3 секции и т.п.) |
| Зона из-за этого выглядит слегка сдвинутой / зубчатой | Честный результат текущей математики |
| В FP после постановки край выглядит ровным сектором | Локальный **repack / align** границ секций под footprint |
| После такого сдвига превью следующих домов тоже «переезжает» | Подложка зависит от уже стоящих зданий |

### 2.3 Smart packing / ring align (полировка — позже)

Это **не ядро**, а F6-полировка:

```text
После Place (или при превью рядом с соседями):
  пересчитать локальные границы кластеров на затронутых кольцах
  так, чтобы footprint стал ровным кольцевым сектором (общие лучи)
  обновить underlay / ghost следующих постановок
```

Пока **не делаем**. Зубчатый span 2/3 секции — ок для MVP.

### 2.4 Presentation дома

| Наблюдение | Вывод |
| --- | --- |
| Зона = пятно секций footprint | Меш annular sectors под зданием |
| Меш дома ≠ растяжка на всю зону | Дом по короткой стороне пятна; у 6×2 зона шире дома |
| 6×2 и 2×2 разные | Разный footprint и разный префаб |

Временный куб заменён. Сейчас:

| Footprint | Prefab (RPGPP) |
| --- | --- |
| House 6×2 | `rpgpp_lt_building_01` |
| House 2×2 | `rpgpp_lt_building_02` |
| Центр (плаза) | `rpgpp_lt_building_03` + `CityCenter` |

Scale: uniform по горизонтальному bounds → короткая сторона pad. Куб остаётся только fallback, если слот prefab пустой.

### 2.5 Valid / invalid placement

| Состояние | Зона | Snap |
| --- | --- | --- |
| Можно ставить | cyan | кольцо **и** лучи (cluster) |
| Нельзя (overlap / out of depth) | **красная** | кольцо **вкл**, лучи **выкл** (угол следует курсору) |
| Клик | только если valid | — |

Occupy пока в Presentation (`HashSet` кластеров). Потом → ECS occupancy.

## 3. Неверные модели (отвергнуто)

```text
A) Город = ringCount × sectorCount фиксированных coarse-клеток (8×24)
B) Глобальный fine angular на сотни лучей как обязательный occupy-атом для всего
C) Один AngularDivisions на все кольца → дома растут по ширине с радиусом
```

## 4. Канон у нас (код)

```text
RadialGridConfig
  InnerRadius, RadialStep, RingCount
  InnerBandClusterCount = 66

TargetClusterWorldWidth = 2π * RingMid(0) / 66

GetClusterCount(ring):
  rings 0–1 → 66
  дальше → round(2π * RingMid(bandStart) / TargetWidth)
  (band = ring/2*2)
```

Place:

```text
build plane → snap (cluster, ring)
→ ExpandClusters (дуга якоря на каждое кольцо глубины)
→ zone mesh по секциям + prefab дома (не на всю зону)
```

Центр временно = `rpgpp_lt_building_03` (`CityCenter`).

## 5. Слои

| Слой | Роль |
| --- | --- |
| Presentation | CityCenter, underlay, ghost zone + building prefab |
| Simulation | config / occupancy (позже) |
| Shell | build catalog, Esc, SimGate.Frozen |

## 6. План

| Шаг | Статус |
| --- | --- |
| Underlay + cluster math | done |
| Place + zone + house prefab | done |
| Occupy / red invalid + ray snap off | done (Presentation) |
| Occupy → ECS | later |
| Rotate | later |
| Roads (свободнее) | later |
| Smart ring align (F6) | later / polish |

---

Связанные: [[04 Simulation]] · [[08 Production ECS]] · [[10 Vertical Slice — Shell + ECS Walkers]] · [[11 Camera & Presentation Scenes]]
