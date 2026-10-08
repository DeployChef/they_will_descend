using System.Collections.Generic;
using FMOD.Studio;
using Unity.Mathematics;
using TheyWillDescend.Presentation.City;
using UnityEngine;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Одна аудио-зона: угловой сектор × радиальная полоса, лежит 1 в 1 поверх
    /// основной сетки города. Одна FMOD Instance на зону, не на каждую ячейку сетки.
    /// </summary>
    public sealed class AudioZone
    {
        /// <summary>Индекс углового сектора.</summary>
        public int Sector { get; }

        /// <summary>Индекс радиальной полосы.</summary>
        public int Radial { get; }

        /// <summary>Средняя мировая позиция зоны (центр конуса).</summary>
        public Vector3 WorldPosition { get; private set; }

        /// <summary>FMOD Instance этой зоны.</summary>
        public EventInstance Instance { get; private set; }

        /// <summary>Активна ли зона (звук воспроизводится).</summary>
        public bool IsActive { get; private set; }

        /// <summary>Видима ли зона (в конусе камеры).</summary>
        public bool IsVisible { get; set; }

        /// <summary>Суммарная активность построек (0–1).</summary>
        public float ActivityLevel { get; private set; }

        /// <summary>Флаги типов построек (0–1).</summary>
        public float HasHouses { get; private set; }
        public float HasWorkshops { get; private set; }
        public float HasMarket { get; private set; }

        /// <summary>Настройки аудио-зон.</summary>
        private readonly AudioZoneSettings _settings;

        /// <summary>Список активных источников звука в этой зоне.</summary>
        private readonly List<BuildingAudioSource> _audioSources = new();

        /// <summary>Центр основной сетки (нужен для случайных точек внутри зоны).</summary>
        private float3 _gridCenter;

        /// <summary>Буфер позиций построек для случайного выбора точки звука.</summary>
        private readonly List<Vector3> _positionCandidates = new();

        public AudioZone(int sector, int radial, AudioZoneSettings settings)
        {
            Sector = sector;
            Radial = radial;
            _settings = settings;
            ActivityLevel = 0f;
            HasHouses = 0f;
            HasWorkshops = 0f;
            HasMarket = 0f;
            IsVisible = false;
            IsActive = false;
        }

        /// <summary>
        /// Устанавливает позицию зоны (центр конуса — середина сектора, середина
        /// радиальной полосы). Радиус считается от внутреннего радиуса основной
        /// сетки, чтобы зоны лежали 1 в 1 поверх неё.
        /// </summary>
        public void SetWorldPosition(float3 gridCenter)
        {
            _gridCenter = gridCenter;

            // Угол середины сектора (не края!).
            var sectorAngle = _settings.SectorAngle * (Sector + 0.5f) * Mathf.Deg2Rad;
            var radius = _settings.InnerRadius + (Radial + 0.5f) * _settings.ZoneDepth;

            WorldPosition = new Vector3(
                Mathf.Sin(sectorAngle),
                0f,
                Mathf.Cos(sectorAngle)
            ) * radius;

            WorldPosition += (Vector3)gridCenter;
        }

        /// <summary>
        /// Создаёт FMOD Instance для этой зоны.
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
                    Debug.LogWarning($"[AudioZone] event '{_settings.EventPath}' not found. FMOD event must be created in FMOD Studio first.");
                }
            }
            catch (System.Exception e)
            {
                if (!_logCreated)
                {
                    _logCreated = true;
                    Debug.LogWarning($"[AudioZone] failed to create instance: {e.Message}");
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
        /// Обновляет 3D-атрибуты инстанса (позиция).
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
        /// Случайная мировая точка для одноразового городского звука внутри зоны:
        /// у случайной живой постройки с небольшим разбросом, а если живых построек
        /// нет — случайная точка самого конуса зоны (сектор × полоса).
        /// </summary>
        public Vector3 GetRandomSoundPosition(float jitterRadius)
        {
            _positionCandidates.Clear();
            for (var i = 0; i < _audioSources.Count; i++)
            {
                var src = _audioSources[i];
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

            var angle = (Sector + UnityEngine.Random.value) * _settings.SectorAngle * Mathf.Deg2Rad;
            var radius = _settings.InnerRadius + (Radial + UnityEngine.Random.value) * _settings.ZoneDepth;
            return (Vector3)_gridCenter + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
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
        /// Обновляет RTPC-параметры на основе содержимого зоны.
        /// </summary>
        public void UpdateRTPC()
        {
            if (!Instance.isValid())
                return;

            Instance.setParameterByName("Cell_Activity", ActivityLevel);
            Instance.setParameterByName("Has_Houses", HasHouses);
            Instance.setParameterByName("Has_Workshops", HasWorkshops);
            Instance.setParameterByName("Has_Market", HasMarket);
        }

        /// <summary>
        /// Обновляет Distance-параметр (audio LOD) — дистанция от камеры до центра зоны,
        /// нормализованная в диапазон 0–100 (диапазон параметра в FMOD).
        /// Вызывается каждый тик видимости.
        /// </summary>
        public void UpdateDistanceRtpc(Vector3 cameraPosition, string rtpcName, float maxDistance)
        {
            if (!Instance.isValid() || string.IsNullOrEmpty(rtpcName))
                return;

            var dist = Vector3.Distance(cameraPosition, WorldPosition);
            var normalized = maxDistance > 0f ? dist / maxDistance * 100f : 0f;
            Instance.setParameterByName(rtpcName, Mathf.Clamp(normalized, 0f, 100f));
        }

        /// <summary>
        /// Активирует или деактивирует зону (запуск/остановка инстанса).
        /// Зона без построек не звучит — инстанс не создаётся.
        /// </summary>
        public void SetActive(bool visible)
        {
            IsVisible = visible;

            // Зона звучит ТОЛЬКО если: (а) в поле зрения, (б) не активна, (в) есть постройки.
            if (visible && !IsActive && !IsEmpty() && ActivityLevel > 0.01f)
            {
                CreateInstance();
                Update3DAttributes();
                UpdateRTPC();
                if (_settings.LogZoneActivity)
                    Debug.Log($"[AudioZone] ACTIVATED: sector {Sector}, radial {Radial}, pos {WorldPosition}");
            }
            else if ((!visible || IsEmpty() || ActivityLevel <= 0.01f) && IsActive)
            {
                ReleaseInstance();
                if (_settings.LogZoneActivity)
                    Debug.Log($"[AudioZone] DEACTIVATED: sector {Sector}, radial {Radial}{(IsEmpty() || ActivityLevel <= 0.01f ? " (no buildings)" : "")}");
            }
        }

        /// <summary>
        /// Дистанционная смерть зоны (audio LOD-куллинг): при dist >= deathDistance
        /// инстанс полностью освобождается — звук не существует, систему не грузит.
        /// Возрождение при dist < deathDistance - hysteresis (если зона видима
        /// и не пуста). Вызывается каждый тик видимости.
        /// </summary>
        public void UpdateDistanceDeath(Vector3 cameraPosition, float deathDistance, float hysteresis)
        {
            var dist = Vector3.Distance(cameraPosition, WorldPosition);

            if (IsActive && dist >= deathDistance)
            {
                ReleaseInstance();
                if (_settings.LogZoneActivity)
                    Debug.Log($"[AudioZone] KILLED by distance ({dist:F0}m): sector {Sector}, radial {Radial}");
                return;
            }

            // Возрождение с гистерезисом: зона видима, не пуста, есть активность
            // (законченные здания), но инстанса нет.
            if (!IsActive && IsVisible && !IsEmpty() && ActivityLevel > 0.01f
                && dist < deathDistance - Mathf.Max(0f, hysteresis))
            {
                CreateInstance();
                Update3DAttributes();
                UpdateRTPC();
                if (_settings.LogZoneActivity)
                    Debug.Log($"[AudioZone] REVIVED by distance ({dist:F0}m): sector {Sector}, radial {Radial}");
            }
        }

        /// <summary>
        /// Добавляет источник звука от постройки.
        /// </summary>
        public void AddAudioSource(BuildingAudioSource source)
        {
            if (!_audioSources.Contains(source))
                _audioSources.Add(source);

            RecalculateParameters();
        }

        /// <summary>
        /// Удаляет источник звука от постройки.
        /// </summary>
        public void RemoveAudioSource(BuildingAudioSource source)
        {
            _audioSources.Remove(source);
            RecalculateParameters();
        }

        /// <summary>
        /// Пересчитывает RTPC-параметры на основе всех источников в зоне.
        /// Учитываются только законченные здания (CountsForAmbience).
        /// </summary>
        private void RecalculateParameters()
        {
            float totalActivity = 0f;
            float houseCount = 0f;
            float workshopCount = 0f;
            float marketCount = 0f;
            var counted = 0;

            for (var i = 0; i < _audioSources.Count; i++)
            {
                var src = _audioSources[i];
                if (src == null || !src.enabled)
                    continue;

                // Строящееся/демонтируемое здание в амбиенсе зоны не участвует:
                // Ambience_Town играет только после COMPLETE.
                if (!src.CountsForAmbience)
                    continue;

                counted++;
                totalActivity += src.ActivityWeight;

                switch (src.BuildingType)
                {
                    case BuildingAudioSourceType.House:
                        houseCount++;
                        break;
                    case BuildingAudioSourceType.Workshop:
                        workshopCount++;
                        break;
                    case BuildingAudioSourceType.Market:
                        marketCount++;
                        break;
                }
            }

            var maxSources = 5f;
            ActivityLevel = Mathf.Min(1f, totalActivity / maxSources);
            HasHouses = Mathf.Min(1f, houseCount / maxSources);
            HasWorkshops = Mathf.Min(1f, workshopCount / maxSources);
            HasMarket = Mathf.Min(1f, marketCount / maxSources);

            // В зоне нет ни одного законченного здания (все строятся или зона
            // опустела) — амбиент глохнет.
            if (counted == 0 && IsActive)
            {
                ReleaseInstance();
                return;
            }

            if (IsActive)
                UpdateRTPC();
        }

        /// <summary>
        /// Пересчитать параметры зоны извне (состав источников изменился —
        /// например, здание перешло в COMPLETE или обратно в стройку).
        /// Зона могла ожить (первое законченное здание) — будим сразу:
        /// ApplyVisibility дёргает SetActive только на СМЕНЕ видимости,
        /// уже видимую спящую зону он не разбудит.
        /// </summary>
        public void Refresh()
        {
            RecalculateParameters();

            // Диагностика: почему зона будится или нет.
            if (_settings.LogZoneActivity)
                Debug.Log($"[AudioZone] REFRESH: sector {Sector}, radial {Radial}, " +
                          $"visible={IsVisible}, active={IsActive}, empty={IsEmpty()}, " +
                          $"activity={ActivityLevel:F2}, sources={_audioSources.Count}");

            if (IsVisible && !IsActive && !IsEmpty() && ActivityLevel > 0.01f)
                SetActive(true);
        }

        /// <summary>
        /// Проверяет, свободна ли зона.
        /// </summary>
        public bool IsEmpty() => _audioSources.Count == 0;

        /// <summary>
        /// Освобождает все ресурсы.
        /// </summary>
        public void Dispose()
        {
            _audioSources.Clear();
            ReleaseInstance();
        }

        /// <summary>
        /// Отрисовка в Gizmos для дебага. Рисуется всегда (не только при выделении).
        /// Цвета: зелёный — видимая и активная, синий — активная, красный — неактивная.
        /// </summary>
        public void OnDrawGizmos(Vector3 center, AudioZoneSettings settings)
        {
            var sectorAngle = settings.SectorAngle * Mathf.Deg2Rad;
            var innerRadius = settings.InnerRadius + Radial * settings.ZoneDepth;
            var outerRadius = settings.InnerRadius + (Radial + 1) * settings.ZoneDepth;
            var angleStart = Sector * sectorAngle;
            var angleEnd = angleStart + sectorAngle;

            // Цвет по состоянию видимости (геометрия, не зависит от FMOD).
            // Зелёный — в поле зрения камеры, красный — за камерой (мёртвое состояние).
            // Если FMOD-инстанс реально звучит — синий оттенок.
            Color color;
            if (IsVisible)
                color = IsActive
                    ? new Color(0f, 1f, 0.3f, 0.8f)          // зелёный — видна и звучит
                    : new Color(0.6f, 1f, 0.3f, 0.6f);       // жёлто-зелёный — видна, FMOD-ивента нет
            else
                color = new Color(1f, 0.15f, 0.15f, 0.35f);  // красный — мёртвое состояние

            Gizmos.color = color;

            var segments = 12;
            var y = center.y + 0.05f; // чуть выше земли, чтобы не z-фights

            // Дуги по внутреннему и внешнему радиусу.
            Vector3 prevInner = default, prevOuter = default, firstInner = default, firstOuter = default;
            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                var angle = angleStart + t * (angleEnd - angleStart);
                var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                var inner = center + dir * innerRadius + new Vector3(0f, y, 0f);
                var outer = center + dir * outerRadius + new Vector3(0f, y, 0f);

                if (i > 0)
                {
                    Gizmos.DrawLine(prevInner, inner);
                    Gizmos.DrawLine(prevOuter, outer);
                    // Спицы-заливка между кольцами.
                    Gizmos.DrawLine(inner, outer);
                }
                else
                {
                    firstInner = inner;
                    firstOuter = outer;
                }

                prevInner = inner;
                prevOuter = outer;
            }

            // Боковые рёбра сектора.
            Gizmos.DrawLine(firstInner, firstOuter);
            Gizmos.DrawLine(prevInner, prevOuter);

            // Маркер центра зоны (позиция FMOD-инстанса): сфера + вертикальная стойка.
            var markerPos = WorldPosition + new Vector3(0f, y + 0.5f, 0f);
            Gizmos.color = IsVisible ? Color.cyan : Color.red;
            Gizmos.DrawSphere(markerPos, 0.3f);
            Gizmos.DrawLine(markerPos, markerPos + Vector3.up * 1.5f);
        }
    }
}
