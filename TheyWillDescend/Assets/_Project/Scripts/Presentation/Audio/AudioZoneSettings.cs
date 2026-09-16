using UnityEngine;
using FMODUnity;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Настройки аудио-зон. Геометрия 1 в 1 с основной сеткой города: тот же
    /// внутренний радиус (InnerRadius) и тот же внешний (InnerRadius + RingCount*RadialStep).
    /// 10 угловых секторов × 5 радиальных полос = 50 ячеек, растянуты равномерно.
    /// </summary>
    [CreateAssetMenu(menuName = "TheyWillDescend/Audio/Audio Zone Settings")]
    public sealed class AudioZoneSettings : ScriptableObject
    {
        [Header("Zone Count")]
        [Tooltip("Угловых секторов (360 / сектор = размер конуса в градусах).")]
        [SerializeField] int angularSectors = 10;

        [Tooltip("Радиальных полос. Кольца основной сетки делятся между ними ровно.")]
        [SerializeField] int radialBands = 5;

        [Header("Grid Extent (1 в 1 с основной сеткой)")]
        [Tooltip("Внутренний радиус = InnerRadius основной сетки. В рантайме берётся из CityGrid, это fallback.")]
        [SerializeField] float gridInnerRadius = 9f;

        [Tooltip("Внешний радиус = InnerRadius + RingCount * RadialStep основной сетки. В рантайме берётся из CityGrid, это fallback.")]
        [SerializeField] float gridOuterRadius = 49.5f;

        [Header("FMOD")]
        [Tooltip("FMOD event для зон. Перетаскивается из FMOD Studio (как у StudioEventEmitter). Банки определяются автоматически.")]
        [SerializeField] EventReference eventReference;

        [Tooltip("Fallback: путь ивента строкой, если EventReference не задан.")]
        [SerializeField] string eventPath = "event:/Ambience_Town";

        [Tooltip("FMOD bus для аудио-сетки.")]
        [SerializeField] string audioBusPath = "bus:/";

        [Header("Audio LOD")]
        [Tooltip("RTPC-параметр дистанции (audio LOD) в ивенте ambience_town.")]
        [SerializeField] string distanceRtpcName = "Distance";

        [Tooltip("Дистанция смерти зоны (м): дальше — инстанс release, звук не существует, систему не нагружает. Возрождение ближе чем Death Distance - Hysteresis.")]
        [SerializeField] float zoneDeathDistance = 100f;

        [Tooltip("Гистерезис возрождения зоны (м), чтобы инстанс не дёргался на границе.")]
        [SerializeField] float zoneDeathHysteresis = 5f;

        [Header("Debug")]
        [Tooltip("Логировать вход/выход зон в консоль.")]
        [SerializeField] bool logZoneActivity = false;

        public int AngularSectors => angularSectors > 0 ? angularSectors : 1;
        public int RadialBands => radialBands > 0 ? radialBands : 1;

        /// <summary>Всего ячеек аудио-сетки (секторы × полосы).</summary>
        public int TotalZones => AngularSectors * RadialBands;

        public float SectorAngle => 360f / AngularSectors;

        /// <summary>Внутренний радиус аудио-сетки = внутренний радиус основной сетки.</summary>
        public float InnerRadius => gridInnerRadius;

        /// <summary>Внешний радиус аудио-сетки = внешний радиус основной сетки.</summary>
        public float OuterRadius => gridOuterRadius;

        /// <summary>Толщина одной радиальной полосы.</summary>
        public float ZoneDepth
        {
            get
            {
                var span = gridOuterRadius - gridInnerRadius;
                return span > 0f ? span / RadialBands : 1f;
            }
        }

        /// <summary>
        /// Дистанция для нормализации Distance RTPC (audio LOD).
        /// Слышимость зон больше НЕ ограничивает — конус камеры решает.
        /// </summary>
        public float MaxDistance => OuterRadius;

        public EventReference EventReference => eventReference;
        public string EventPath => eventPath;
        public string AudioBusPath => audioBusPath;
        public string DistanceRtpcName => distanceRtpcName;
        public float ZoneDeathDistance => zoneDeathDistance;
        public float ZoneDeathHysteresis => zoneDeathHysteresis;
        public bool LogZoneActivity => logZoneActivity;

        /// <summary>
        /// Применяет геометрию основной сетки. Возвращает true, если она изменилась
        /// (значит зоны нужно перестроить).
        /// </summary>
        public bool SetGridExtent(float innerRadius, float outerRadius)
        {
            if (Mathf.Abs(innerRadius - gridInnerRadius) < 0.001f
                && Mathf.Abs(outerRadius - gridOuterRadius) < 0.001f)
                return false;

            gridInnerRadius = innerRadius;
            gridOuterRadius = outerRadius;
            return true;
        }
    }
}
