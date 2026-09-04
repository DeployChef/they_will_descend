using UnityEngine;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Настройки аудио-зон. 12 угловых секторов × 10 дистанционных зон = 120 зон.
    /// Каждая зона = конус 30° × дистанционное кольцо ~12м (0-120м).
    /// </summary>
    [CreateAssetMenu(menuName = "TheyWillDescend/Audio/Audio Zone Settings")]
    public sealed class AudioZoneSettings : ScriptableObject
    {
        [Header("Zone Count")]
        [Tooltip("Угловых секторов (360 / сектор = размер конуса в градусах).")]
        [SerializeField] int angularSectors = 12;

        [Tooltip("Дистанционных зон (0-120м делится на это кол-во).")]
        [SerializeField] int radialZones = 10;

        [Header("Visibility")]
        [Tooltip("Максимальная дистанция (м).")]
        [SerializeField] float maxDistance = 120f;

        [Header("FMOD")]
        [Tooltip("FMOD event path для зон.")]
        [SerializeField] string eventPath = "event:/audio/city/ambience_town";

        [Tooltip("FMOD bus для аудио-сетки.")]
        [SerializeField] string audioBusPath = "bus:/";

        [Header("Audio LOD")]
        [Tooltip("RTPC-параметр дистанции (audio LOD) в ивенте ambience_town.")]
        [SerializeField] string distanceRtpcName = "Distance";

        [Header("Debug")]
        [Tooltip("Логировать вход/выход зон в консоль.")]
        [SerializeField] bool logZoneActivity = true;

        public int AngularSectors => angularSectors;
        public int RadialZones => radialZones;
        public float MaxDistance => maxDistance;
        public float SectorAngle => 360f / angularSectors;
        public float ZoneDepth => maxDistance / radialZones;
        public string EventPath => eventPath;
        public string AudioBusPath => audioBusPath;
        public string DistanceRtpcName => distanceRtpcName;
        public bool LogZoneActivity => logZoneActivity;
    }
}
