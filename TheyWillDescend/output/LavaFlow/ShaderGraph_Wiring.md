# Lava Flow Map — подключение к SG_Lava

Карта: Assets/_Project/Art/Textures/Lava/Lava_FlowMap_01.png.
Это рассчитанное поле направлений для UV0 плейна с центром (0.5,0.5), а не цвет поверхности.
Карта подготовлена и импортирована; сам SG_Lava пока не изменён.
R и G содержат направление и величину скорости, декодирование: RG * 2 - 1.
Вектор не нормализовать: его длина задаёт скорость.
Не использовать UV масштабирования маски корки: карта течения должна оставаться привязана к UV0.
Поле основано на форме кольца; точные контуры скал в него не запекались.

## 1. Чтение направления
1. Добавить Texture2D property FlowMap, Reference _FlowMap; назначить карту как Default.
2. Property -> Sample Texture 2D / Texture.
3. UV node Channel UV0 -> Sample / UV.
4. Sample / RGBA -> Split.
5. Split / R -> Combine / R; Split / G -> Combine / G.
6. Combine / RG -> Multiply / A; Multiply / B = 2.
7. Multiply / Out -> Subtract / A; Subtract / B = 1.
Полученный Vector2 = FlowDirection. Пока не подключать к Fragment.

## 2. Две фазы
Добавить Float properties:
- FlowCycleSpeed (_FlowCycleSpeed), default 0.12 (циклов/сек).
- FlowStrength (_FlowStrength), default 0.06 (в текущих UV жидких текстур).
Числа — стартовые для подбора, не физические параметры.

Time / Time -> Multiply (B = FlowCycleSpeed) -> Fraction = PhaseA.
PhaseA -> Add (B = 0.5) -> Fraction = PhaseB.
FlowDirection -> Multiply (B = FlowStrength) = FlowOffset.

PhaseA -> Subtract (B = 0.5) = CenteredA.
PhaseB -> Subtract (B = 0.5) = CenteredB.
FlowOffset * CenteredA = OffsetA.
FlowOffset * CenteredB = OffsetB.

BaseUV = исходные координаты жидких текстур ДО прибавления старого Time Offset.
BaseUV - OffsetA = UV_A.
BaseUV - OffsetB = UV_B.

Blend = Absolute(PhaseA * 2 - 1).

## 3. Сэмплы и выходы
Для каждой карты жидкой лавы (BaseColor, Emission, Normal):
- два Sample Texture 2D одной и той же карты;
- первый UV = UV_A, второй UV = UV_B;
- Lerp: A = первый sample, B = второй sample, T = Blend.
Normal samples: Type Normal; после Lerp нормализовать.

Смешанные BaseColor/Emission/Normal заменить только на соответствующих входах A
существующих Lerp «лава / корка».
Входы B с коркой и T с маской оставить.
Normal после смешивания лава / корка также нормализовать.
Все жидкие карты используют одну фазу, одно направление и одинаковые базовые UV.

Только после подключения двух фаз убрать старый Time Offset из UV жидких карт.
Одна бесконечно растущая фаза будет растягивать рисунок; сброс одной фазы создаёт скачок.

## Проверка
- Корка у берегов остаётся неподвижной.
- Расплав меняет направление по кольцу, на отдельных участках есть завихрения.
- Нет заметного скачка через каждые ~8.33 секунды (при CycleSpeed 0.12).
- Нет разъезда BaseColor, Normal и Emission.
- Если искажение велико, уменьшить FlowStrength; если быстро — FlowCycleSpeed.
- Итог оценивать из игровой камеры и вблизи; текущая численная карта сама по себе
  не подтверждает качество анимации.

