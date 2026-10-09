using System.Collections.Generic;
using FMOD.Studio;
using UnityEngine;
using TheyWillDescend.Presentation.City;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Одна аудио-ячейка: динамический кластер построек. Рождается вместе с первой
    /// постройкой, вмещает Capacity построек, при переполнении открывается новая.
    /// Форма и размер ячейки задаются её постройками, FMOD Instance стоит в
    /// центроиде кластера. Одна FMOD Instance на ячейку.
    /// </summary>
    public sealed class AudioCell
    {
        /// <summary>Сквозной id ячейки — для логов и подписей в гизмо.</summary>
        public int Id { get; }

        /// <summary>Центроид кластера — мировая позиция FMOD-инстанса.</summary>
        public Vector3 WorldPosition { get; private set; }

        /// <summary>Радиус кластера: дальность центроида до самой дальней постройки.</summary>
        public float Radius { get; private set; } = 1f;

        /// <summary>FMOD Instance этой ячейки.</summary>
        public EventInstance Instance { get; private set; }

        /// <summary>Активна ли ячейка (звук воспроизводится).</summary>
        public bool IsActive { get; private set; }

        /// <summary>Видима ли ячейка (в конусе камеры).</summary>
        public bool IsVisible { get; set; }

        /// <summary>Ячейка удалена из реестра — ссылки на неё больше не валидны.</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>Кулдаун планировщика случайных звуков (unscaled time, до которого ячейка не тянет жребий).</summary>
        public float CooldownUntil { get; set; }

        /// <summary>Сколько построек вмещает ячейка.</summary>
        public int Capacity { get; }

        /// <summary>Суммарная активность построек (0–1). Внутренний метрик активации, в FMOD не идёт.</summary>
        public float ActivityLevel { get; private set; }

        /// <summary>Есть ли в ячейке готовый жилой дом (RTPC IF_VILLAGE_SECTORE: 0 — нет, 1 — есть).</summary>
        public float VillageFlag { get; private set; }

        /// <summary>Плотность жилой застройки (RTPC VILLAGE_DESTINY): число готовых жилых домов, потолок Capacity.</summary>
        public float VillageDensity { get; private set; }

        /// <summary>Число готовых построек всех типов в ячейке.</summary>
        public int CompletedCount { get; private set; }

        /// <summary>Число готовых жилых домов в ячейке.</summary>
        public int HouseCount { get; private set; }

        /// <summary>Настройки аудио-ячеек.</summary>
        private readonly AudioZoneSettings _settings;

        /// <summary>Постройки, закреплённые за этой ячейкой.</summary>
        private readonly List<BuildingAudioSource> _sources = new();

        /// <summary>Буфер позиций построек для случайного выбора точки звука.</summary>
        private readonly List<Vector3> _positionCandidates = new();

        public AudioCell(int id, AudioZoneSettings settings)
        {
            Id = id;
            _settings = settings;
            Capacity = settings.CellCapacity;
        }

        /// <summary>Постройки ячейки (только для чтения).</summary>
        public IReadOnlyList<BuildingAudioSource> Sources => _sources;

        /// <summary>Число закреплённых построек (включая строящиеся).</summary>
        public int Count => _sources.Count;

        /// <summary>Ячейка пуста — подлежит удалению из реестра.</summary>
        public bool IsEmpty => _sources.Count == 0;

        /// <summary>Ячейка заполнена — новые постройки в неё не принимаются.</summary>
        public bool IsFull => _sources.Count >= Capacity;

        /// <summary>
        /// Расстояние привязки новой постройки к ячейке: минимум из дистанции до
        /// центроида и дистанции до ближайшей постройки ячейки. Гибрид даёт и
        /// компактные сгустки, и цепочки вдоль улицы.
        /// </summary>
        public float DistanceTo(Vector3 worldPos)
        {
            if (_sources.Count == 0)
                return Vector3.Distance(worldPos, WorldPosition);

            var best = Vector3.Distance(worldPos, WorldPosition);

            for (var i = 0; i < _sources.Count; i++)
            {
                var src = _sources[i];
                if (src == null)
                    continue;

                var d = Vector3.Distance(worldPos, src.transform.position);
                if (d < best)
                    best = d;
            }

            return best;
        }

        /// <summary>
        /// Создаёт FMOD Instance для этой ячейки.
        /// </summary>
        public void CreateInstance()
        {
            if (Instance.isValid())
                return;

            try
            {
                // Приоритет у EventReference (drag-and-drop из FMOD Studio),
                // строковый путь — fallback.
                if (!_settings.EventReference.IsNull)
                    Instance = FMODUnity.RuntimeManager.CreateInstance(_settings.EventReference);
                else
                    Instance = FMODUnity.RuntimeManager.CreateInstance(_settings.EventPath);

                if (Instance.isValid())
                {
                    Instance.start();
                    IsActive = true;
                }
                else if (!_logCreated)
                {
                    _logCreated = true;
                    Debug.LogWarning($"[AudioCell] event '{_settings.EventPath}' not found. FMOD event must be created in FMOD Studio first.");
                }
            }
            catch (System.Exception e)
            {
                if (!_logCreated)
                {
                    _logCreated = true;
                    Debug.LogWarning($"[AudioCell] failed to create instance: {e.Message}");
                }
            }
        }

        static bool _logCreated;

        /// <summary>
        /// Освобождает FMOD Instance.
        /// </summary>
        public void ReleaseInstance()
        {
            if (!Instance.isValid())
                return;

            Instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            Instance.release();
            Instance = default;
            IsActive = false;
        }

        /// <summary>
        /// Обновляет 3D-атрибуты инстанса (позиция = центроид кластера).
        /// </summary>
        public void Update3DAttributes()
        {
            if (!Instance.isValid())
                return;

            var attributes = new FMOD.ATTRIBUTES_3D
            {
                position = new FMOD.VECTOR { x = WorldPosition.x, y = WorldPosition.y, z = WorldPosition.z },
                velocity = new FMOD.VECTOR { x = 0f, y = 0f, z = 0f },
                forward = new FMOD.VECTOR { x = 0f, y = 0f, z = 1f },
                up = new FMOD.VECTOR { x = 0f, y = 1f, z = 0f }
            };

            Instance.set3DAttributes(attributes);
        }

        /// <summary>
        /// Случайная мировая точка для одноразового городского звука внутри ячейки:
        /// у случайной готовой постройки с небольшим разбросом, а если готовых
        /// построек нет — случайная точка самого кластера вокруг центроида.
        /// </summary>
        public Vector3 GetRandomSoundPosition(float jitterRadius)
        {
            _positionCandidates.Clear();
            for (var i = 0; i < _sources.Count; i++)
            {
                var src = _sources[i];
                if (src == null || !src.enabled || !src.gameObject.activeInHierarchy)
                    continue;

                // Случайные городские звуки — только у законченных зданий.
                if (!src.CountsForAmbience)
                    continue;

                _positionCandidates.Add(src.transform.position);
            }

            if (_positionCandidates.Count > 0)
            {
                var buildingPos = _positionCandidates[UnityEngine.Random.Range(0, _positionCandidates.Count)];
                return buildingPos + RandomOffsetXZ(jitterRadius);
            }

            return WorldPosition + RandomOffsetXZ(Mathf.Max(1f, Radius));
        }

        /// <summary>Случайное горизонтальное смещение внутри круга заданного радиуса.</summary>
        static Vector3 RandomOffsetXZ(float radius)
        {
            if (radius <= 0f)
                return Vector3.zero;

            var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var r = radius * Mathf.Sqrt(UnityEngine.Random.value);
            return new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
        }

        /// <summary>
        /// Обновляет RTPC-параметры содержимого ячейки.
        /// IF_VILLAGE_SECTORE — флаг жилой застройки, VILLAGE_DESTINY — её плотность
        /// (число готовых жилых домов, 0..Capacity).
        /// Другие классы построек получат свои параметры по той же схеме: счётчик
        /// нужного типа в RecalculateParameters() + setParameterByName здесь.
        /// </summary>
        public void UpdateRTPC()
        {
            if (!Instance.isValid())
                return;

            Instance.setParameterByName("IF_VILLAGE_SECTORE", VillageFlag);
            Instance.setParameterByName("VILLAGE_DESTINY", VillageDensity);
        }

        /// <summary>
        /// Обновляет Distance-параметр (audio LOD) — дистанция от камеры до центра
        /// ячейки, нормализованная в диапазон 0–100 (диапазон параметра в FMOD).
        /// Вызывается каждый тик видимости.
        /// </summary>
        public void UpdateDistanceRtpc(Vector3 cameraPosition, string rtpcName, float range)
        {
            if (!Instance.isValid() || string.IsNullOrEmpty(rtpcName))
                return;

            var dist = Vector3.Distance(cameraPosition, WorldPosition);
            var normalized = range > 0f ? dist / range * 100f : 0f;
            Instance.setParameterByName(rtpcName, Mathf.Clamp(normalized, 0f, 100f));
        }

        /// <summary>
        /// Активирует или деактивирует ячейку (запуск/остановка инстанса).
        /// Ячейка без готовых построек не звучит — инстанс не создаётся.
        /// </summary>
        public void SetActive(bool visible)
        {
            IsVisible = visible;

            if (visible && !IsActive && CanSound())
            {
                CreateInstance();
                Update3DAttributes();
                UpdateRTPC();
                if (_settings.LogZoneActivity)
                    Debug.Log($"[AudioCell] ACTIVATED: cell {Id}, pos {WorldPosition}, sources {_sources.Count}");
            }
            else if ((!visible || !CanSound()) && IsActive)
            {
                ReleaseInstance();
                if (_settings.LogZoneActivity)
                    Debug.Log($"[AudioCell] DEACTIVATED: cell {Id}{(!CanSound() ? " (no completed buildings)" : "")}");
            }
        }

        /// <summary>
        /// Дистанционная смерть ячейки (audio LOD-куллинг): при dist >= deathDistance
        /// инстанс полностью освобождается — звук не существует, систему не грузит.
        /// Возрождение при dist < deathDistance - hysteresis (если ячейка видима
        /// и в ней есть готовые постройки). Вызывается каждый тик видимости.
        /// </summary>
        public void UpdateDistanceDeath(Vector3 cameraPosition, float deathDistance, float hysteresis)
        {
            var dist = Vector3.Distance(cameraPosition, WorldPosition);

            if (IsActive && dist >= deathDistance)
            {
                ReleaseInstance();
                if (_settings.LogZoneActivity)
                    Debug.Log($"[AudioCell] KILLED by distance ({dist:F0}m): cell {Id}");
                return;
            }

            if (!IsActive && IsVisible && CanSound()
                && dist < deathDistance - Mathf.Max(0f, hysteresis))
            {
                CreateInstance();
                Update3DAttributes();
                UpdateRTPC();
                if (_settings.LogZoneActivity)
                    Debug.Log($"[AudioCell] REVIVED by distance ({dist:F0}m): cell {Id}");
            }
        }

        /// <summary>Ячейка может звучать: видимость не смотрим — это про состав.</summary>
        bool CanSound() => CompletedCount > 0 && ActivityLevel > 0.01f;

        /// <summary>
        /// Закрепляет постройку за ячейкой.
        /// </summary>
        public void AddSource(BuildingAudioSource source)
        {
            if (source == null || IsDisposed)
                return;

            if (!_sources.Contains(source))
                _sources.Add(source);

            RecalculateParameters();
        }

        /// <summary>
        /// Отвязывает постройку от ячейки.
        /// </summary>
        public void RemoveSource(BuildingAudioSource source)
        {
            if (source == null)
                return;

            if (_sources.Remove(source))
                RecalculateParameters();
        }

        /// <summary>
        /// Пересчитывает состав: центроид, радиус, счётчики по типам, RTPC.
        /// В RTPC идут только законченные здания (CountsForAmbience).
        /// </summary>
        private void RecalculateParameters()
        {
            RecalculateShape();

            float totalActivity = 0f;
            var houseCount = 0;
            var counted = 0;

            for (var i = 0; i < _sources.Count; i++)
            {
                var src = _sources[i];
                if (src == null || !src.enabled)
                    continue;

                if (!src.CountsForAmbience)
                    continue;

                counted++;
                totalActivity += src.ActivityWeight;

                // Счётчики по классам построек. Новый класс (Factory, Cultural,
                // Energy, ...) = ещё один счётчик здесь + свой RTPC в UpdateRTPC().
                if (src.BuildingType == BuildingAudioSourceType.House)
                {
                    houseCount++;
                }
            }

            CompletedCount = counted;
            HouseCount = houseCount;
            ActivityLevel = Mathf.Min(1f, totalActivity / Capacity);

            // Флаг жилой застройки: есть хоть один готовый жилой дом — 1.
            VillageFlag = houseCount > 0 ? 1f : 0f;

            // Плотность жилого класса: сколько готовых домов в ячейке, потолок —
            // вместимость ячейки (диапазон дискретного VILLAGE_DESTINY в FMOD).
            VillageDensity = Mathf.Min(houseCount, Capacity);

            // Готовых построек не осталось (все строятся или ячейка опустела) —
            // амбиент глохнет.
            if (counted == 0 && IsActive)
            {
                ReleaseInstance();
                return;
            }

            if (IsActive)
                UpdateRTPC();
        }

        /// <summary>
        /// Пересчитывает центроид и радиус кластера по позициям построек.
        /// Инстанс FMOD всегда стоит посреди ячейки.
        /// </summary>
        private void RecalculateShape()
        {
            var sum = Vector3.zero;
            var live = 0;

            for (var i = 0; i < _sources.Count; i++)
            {
                var src = _sources[i];
                if (src == null)
                    continue;

                sum += src.transform.position;
                live++;
            }

            if (live == 0)
            {
                Radius = 1f;
                return;
            }

            WorldPosition = sum / live;

            var maxSq = 0f;
            for (var i = 0; i < _sources.Count; i++)
            {
                var src = _sources[i];
                if (src == null)
                    continue;

                var d = (src.transform.position - WorldPosition).sqrMagnitude;
                if (d > maxSq)
                    maxSq = d;
            }

            Radius = Mathf.Max(1f, Mathf.Sqrt(maxSq));
        }

        /// <summary>
        /// Пересчитать параметры ячейки извне (состав изменился — например, здание
        /// перешло в COMPLETE или обратно в стройку). Ячейка могла ожить (первое
        /// законченное здание) — будим сразу: ApplyVisibility дёргает SetActive
        /// только на СМЕНЕ видимости, уже видимую спящую ячейку он не разбудит.
        /// </summary>
        public void Refresh()
        {
            RecalculateParameters();

            if (_settings.LogZoneActivity)
                Debug.Log($"[AudioCell] REFRESH: cell {Id}, visible={IsVisible}, active={IsActive}, " +
                          $"empty={IsEmpty}, completed={CompletedCount}, activity={ActivityLevel:F2}, " +
                          $"sources={_sources.Count}, centroid={WorldPosition}");

            if (IsVisible && !IsActive && CanSound())
                SetActive(true);
        }

        /// <summary>
        /// Освобождает все ресурсы. Ячейка помечается мёртвой — ссылки на неё
        /// у построек больше не используются.
        /// </summary>
        public void Dispose()
        {
            _sources.Clear();
            ReleaseInstance();
            IsDisposed = true;
        }

        // ===== Gizmos =====

        /// <summary>Scratch-буферы отрисовки (гизмо однопоточны, аллокации не в рантайме).</summary>
        static readonly List<Vector3> s_hullInput = new();
        static readonly List<Vector3> s_hull = new();
        static readonly List<Vector3> s_lower = new();
        static readonly List<Vector3> s_upper = new();
        static readonly List<Vector3> s_sorted = new();

        /// <summary>Буфер точек контура (Handles требует массив точной длины).</summary>
        static Vector3[] s_loopBuf;

        /// <summary>Буфер одного отрезка.</summary>
        static readonly Vector3[] s_lineBuf = new Vector3[2];

        /// <summary>
        /// Гизмо ячейки: толстый округлый контур кластера, спицы от центра к каждой
        /// постройке, пунктирный круг радиуса привязки, маркер центра инстанса.
        /// Толщина — через Handles: URP игнорирует Gizmos.lineWidth.
        /// </summary>
        public void OnDrawGizmos(AudioZoneSettings settings)
        {
            if (_sources.Count == 0)
                return;

            var stateColor = StateColor();
            var width = settings.GizmoLineWidth;
            var y = WorldPosition.y;

            if (settings.ShowAttachRadius)
                DrawAttachRadius(settings, stateColor, y);

            BuildOutline(settings, s_hull);

            if (s_hull.Count >= 2)
            {
                // Нижний контур — основной, самый толстый.
                DrawThickLoop(s_hull, stateColor, width, y + 0.05f);

                // Верхний контур + стойки: читается как объёмная клетка.
                DrawThickLoop(s_hull, WithAlpha(stateColor, stateColor.a * 0.55f), Mathf.Max(1f, width * 0.5f), y + 1.4f);
                DrawPosts(s_hull, WithAlpha(stateColor, stateColor.a * 0.45f), y);
            }

            DrawSpokes(settings, stateColor, y);
            DrawCenterMarker(settings, stateColor, y);

#if UNITY_EDITOR
            if (settings.ShowZoneLabels)
                DrawLabel(settings);
#endif
        }

        /// <summary>Цвет состояния: зелёный — звучит, жёлтый — видна и спит, красный — мертва.</summary>
        Color StateColor()
        {
            if (!IsVisible)
                return new Color(1f, 0.2f, 0.2f, 0.45f);

            return IsActive
                ? new Color(0.1f, 1f, 0.35f, 0.95f)
                : new Color(1f, 0.85f, 0.2f, 0.8f);
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, Mathf.Clamp01(a));

        /// <summary>
        /// Оболочка ячейки: округлая оболочка кругов радиуса отступа вокруг каждой
        /// постройки. Отсюда произвольная форма — сгусток, цепочка, подкова.
        /// </summary>
        void BuildOutline(AudioZoneSettings settings, List<Vector3> hull)
        {
            hull.Clear();
            s_hullInput.Clear();

            var pad = settings.GizmoHullPadding;

            for (var i = 0; i < _sources.Count; i++)
            {
                var src = _sources[i];
                if (src == null)
                    continue;

                AddCircle(s_hullInput, src.transform.position, pad);
            }

            // Одна постройка (или все уничтожены) — круг вокруг центроида.
            if (s_hullInput.Count == 0)
                AddCircle(s_hullInput, WorldPosition, Mathf.Max(pad, Radius));

            ConvexHullXZ(s_hullInput, hull);
        }

        /// <summary>Восемь точек окружности в XZ вокруг центра.</summary>
        static void AddCircle(List<Vector3> into, Vector3 center, float radius)
        {
            const int segments = 8;
            for (var i = 0; i < segments; i++)
            {
                var a = (float)i / segments * Mathf.PI * 2f;
                into.Add(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
        }

        /// <summary>
        /// Оболочка Тёрринга (monotone chain) по плоскости XZ. Вход — точки кругов
        /// вокруг построек, выход — округлый контур кластера против часовой.
        /// </summary>
        static void ConvexHullXZ(List<Vector3> points, List<Vector3> hull)
        {
            hull.Clear();
            if (points.Count < 3)
            {
                hull.AddRange(points);
                return;
            }

            s_sorted.Clear();
            s_sorted.AddRange(points);
            s_sorted.Sort((a, b) => Mathf.Abs(a.x - b.x) > 1e-5f ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));

            s_lower.Clear();
            for (var i = 0; i < s_sorted.Count; i++)
            {
                var p = s_sorted[i];
                while (s_lower.Count >= 2
                       && CrossXZ(s_lower[s_lower.Count - 2], s_lower[s_lower.Count - 1], p) <= 0f)
                    s_lower.RemoveAt(s_lower.Count - 1);
                s_lower.Add(p);
            }

            s_upper.Clear();
            for (var i = s_sorted.Count - 1; i >= 0; i--)
            {
                var p = s_sorted[i];
                while (s_upper.Count >= 2
                       && CrossXZ(s_upper[s_upper.Count - 2], s_upper[s_upper.Count - 1], p) <= 0f)
                    s_upper.RemoveAt(s_upper.Count - 1);
                s_upper.Add(p);
            }

            s_lower.RemoveAt(s_lower.Count - 1);
            s_upper.RemoveAt(s_upper.Count - 1);

            hull.AddRange(s_lower);
            hull.AddRange(s_upper);
            if (hull.Count == 0)
                hull.AddRange(s_sorted);
        }

        /// <summary>Знак поворота трёх точек в XZ.</summary>
        static float CrossXZ(Vector3 o, Vector3 a, Vector3 b)
            => (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);

        /// <summary>Замкнутый контур заданной толщины на абсолютной высоте y.</summary>
        static void DrawThickLoop(List<Vector3> points, Color color, float width, float y)
        {
            var count = points.Count;
            if (count < 2)
                return;

            // Handles.DrawAAPolyLine принимает только Vector3[] точной длины —
            // буфер переиспользуется, новый массив только при смене числа точек.
            if (s_loopBuf == null || s_loopBuf.Length != count + 1)
                s_loopBuf = new Vector3[count + 1];

            for (var i = 0; i < count; i++)
            {
                var p = points[i];
                s_loopBuf[i] = new Vector3(p.x, y, p.z);
            }
            s_loopBuf[count] = s_loopBuf[0];

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.DrawAAPolyLine(width, s_loopBuf);
#else
            Gizmos.color = color;
            for (var i = 1; i < s_loopBuf.Length; i++)
                Gizmos.DrawLine(s_loopBuf[i - 1], s_loopBuf[i]);
#endif
        }

        /// <summary>Вертикальные стойки по углам оболочки (каждая вторая точка).</summary>
        void DrawPosts(List<Vector3> points, Color color, float y)
        {
            for (var i = 0; i < points.Count; i += 2)
            {
                var p = points[i];
                DrawThickLine(new Vector3(p.x, y + 0.05f, p.z), new Vector3(p.x, y + 1.4f, p.z), color, 1.5f);
            }
        }

        /// <summary>
        /// Спицы от центра ячейки к каждой постройке — сразу видно, что входит
        /// в кластер, а что соседняя ячейка.
        /// </summary>
        void DrawSpokes(AudioZoneSettings settings, Color color, float y)
        {
            var hub = new Vector3(WorldPosition.x, y + 0.6f, WorldPosition.z);
            var spokeWidth = Mathf.Max(1.5f, settings.GizmoLineWidth * 0.55f);

            for (var i = 0; i < _sources.Count; i++)
            {
                var src = _sources[i];
                if (src == null)
                    continue;

                var ready = src.CountsForAmbience;
                var spokeColor = ready ? WithAlpha(color, color.a * 0.8f) : new Color(0.65f, 0.65f, 0.7f, 0.5f);
                DrawThickLine(hub, src.transform.position + Vector3.up * 0.05f, spokeColor, spokeWidth);

                // Маркер постройки: готовая — яркая сфера, стройка — тусклая.
                Gizmos.color = spokeColor;
                Gizmos.DrawWireSphere(src.transform.position + Vector3.up * 0.4f, ready ? 0.35f : 0.22f);
            }
        }

        /// <summary>Пунктирный круг радиуса привязки вокруг центроида.</summary>
        void DrawAttachRadius(AudioZoneSettings settings, Color color, float y)
        {
            const int segments = 40;
            var r = settings.AttachRadius;
            var plane = new Vector3(WorldPosition.x, y + 0.02f, WorldPosition.z);
            Vector3 prev = default;
            var started = false;

            for (var i = 0; i <= segments; i++)
            {
                var a = (float)i / segments * Mathf.PI * 2f;
                var p = plane + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;

                // Пунктир: рисуем через сегмент.
                if (started && (i & 1) == 0)
                    DrawThickLine(prev, p, WithAlpha(color, 0.22f), 1.5f);

                prev = p;
                started = true;
            }
        }

        /// <summary>Маркер центра ячейки: позиция FMOD-инстанса (сфера + стойка).</summary>
        void DrawCenterMarker(AudioZoneSettings settings, Color color, float y)
        {
            var basePos = new Vector3(WorldPosition.x, y + 0.1f, WorldPosition.z);

            Gizmos.color = IsVisible ? Color.cyan : Color.red;
            Gizmos.DrawWireSphere(basePos + Vector3.up * 0.6f, 0.4f);

            DrawThickLine(basePos, basePos + Vector3.up * 2.4f, WithAlpha(color, 0.9f),
                Mathf.Max(2f, settings.GizmoLineWidth));
        }

        /// <summary>Отрезок заданной толщины (Handles в редакторе, Gizmos в билде).</summary>
        static void DrawThickLine(Vector3 a, Vector3 b, Color color, float width)
        {
            s_lineBuf[0] = a;
            s_lineBuf[1] = b;

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.DrawAAPolyLine(width, s_lineBuf);
#else
            Gizmos.color = color;
            Gizmos.DrawLine(a, b);
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Подпись ячейки в Scene View: состав, состояние и RTPC — отправленное
        /// значение против прочитанного обратно из FMOD. Readback показывает,
        /// дошло ли значение до графа или FMOD его отбросил.
        /// </summary>
        void DrawLabel(AudioZoneSettings settings)
        {
            if (!Application.isPlaying)
                return;

            var sentIf = VillageFlag;
            var sentDestiny = VillageDensity;
            var readIf = 0f;
            var readDestiny = 0f;
            var readDistance = 0f;
            var readOk = false;

            if (Instance.isValid())
            {
                Instance.getParameterByName("IF_VILLAGE_SECTORE", out readIf);
                Instance.getParameterByName("VILLAGE_DESTINY", out readDestiny);
                Instance.getParameterByName(settings.DistanceRtpcName, out readDistance);
                readOk = true;
            }

            var state = !IsVisible ? "DEAD" : (IsActive ? "PLAY" : "IDLE");
            var text = $"C{Id}  {_sources.Count}/{Capacity}  {state}  houses={HouseCount} completed={CompletedCount}\n" +
                       $"IF_VILLAGE_SECTORE sent={sentIf:0} read={readIf:0}   " +
                       $"VILLAGE_DESTINY sent={sentDestiny:0} read={readDestiny:0}\n" +
                       $"dist={readDistance:F1} r={Radius:F1}";

            if (!readOk)
                text += "  (no FMOD instance)";

            UnityEditor.Handles.Label(WorldPosition + Vector3.up * 3.2f, text, LabelStyle(StateColor()));
        }

        /// <summary>Кэш стиля подписи (пересобирается при смене цвета).</summary>
        static GUIStyle s_labelStyle;
        static Color s_labelColor;

        /// <summary>Стиль подписи ячейки: цвет совпадает с цветом состояния.</summary>
        static GUIStyle LabelStyle(Color color)
        {
            if (s_labelStyle == null)
            {
                s_labelStyle = new GUIStyle(UnityEditor.EditorStyles.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                };
            }

            if (s_labelColor != color)
            {
                s_labelColor = color;
                s_labelStyle.normal.textColor = color;
            }

            return s_labelStyle;
        }
#endif
    }
}
