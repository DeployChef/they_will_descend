using UnityEngine;
using FMODUnity;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Настройки аудио-ячеек. Полярной сетки больше нет: ячейка — динамический
    /// кластер построек. Первая постройка рождает ячейку, дальше постройки
    /// притягиваются к ближайшей неполной ячейке в пределах AttachRadius
    /// (по центроиду ИЛИ по любой её постройке). Набрав CellCapacity построек,
    /// ячейка закрывается — следующая постройка открывает новую.
    /// Форма и размер ячейки = её постройки, FMOD-инстанс стоит в центроиде.
    /// </summary>
    [CreateAssetMenu(menuName = "TheyWillDescend/Audio/Audio Zone Settings")]
    public sealed class AudioZoneSettings : ScriptableObject
    {
        [Header("Cells")]
        [Tooltip("Сколько построек вмещает одна ячейка. На лимите следующая постройка открывает новую ячейку.")]
        [SerializeField] int cellCapacity = 6;

        [Tooltip("Радиус привязки (м). Новая постройка входит в ячейку, если ближе этого расстояния до её центроида ИЛИ до любой её постройки. Дальше — новая ячейка.")]
        [SerializeField] float attachRadius = 12f;

        [Header("FMOD")]
        [Tooltip("FMOD event для зон. Перетаскивается из FMOD Studio (как у StudioEventEmitter). Банки определяются автоматически.")]
        [SerializeField] EventReference eventReference;

        [Tooltip("Fallback: путь ивента строкой, если EventReference не задан. Ивент лежит в папке AMBIENCE.")]
        [SerializeField] string eventPath = "event:/AMBIENCE/Ambience_Town";

        [Tooltip("FMOD bus для аудио-сетки.")]
        [SerializeField] string audioBusPath = "bus:/";

        [Header("Audio LOD")]
        [Tooltip("RTPC-параметр дистанции (audio LOD) в ивенте Ambience_Town.")]
        [SerializeField] string distanceRtpcName = "Distance";

        [Tooltip("Диапазон нормализации Distance RTPC (м): дистанция камеры до центра ячейки делится на него.")]
        [SerializeField] float distanceRtpcRange = 49.5f;

        [Tooltip("Дистанция смерти ячейки (м): дальше — инстанс release, звук не существует, систему не нагружает. Возрождение ближе чем Death Distance - Hysteresis.")]
        [SerializeField] float zoneDeathDistance = 100f;

        [Tooltip("Гистерезис возрождения ячейки (м), чтобы инстанс не дёргался на границе.")]
        [SerializeField] float zoneDeathHysteresis = 5f;

        [Header("Random Town SFX (живчик города)")]
        [Tooltip("Одноразовый ивент городских случайностей. Лежит в том же банке, что и основной амбиент. Перетаскивается из FMOD Studio.")]
        [SerializeField] EventReference sfxRandomEventReference;

        [Tooltip("Fallback: путь ивента строкой, если EventReference не задан. Ивент лежит в папке AMBIENCE.")]
        [SerializeField] string sfxRandomEventPath = "event:/AMBIENCE/Ambience_Town_SFX_Random";

        [Tooltip("Вероятность выстрела ячейки в тик планировщика (независимый бросок на каждую активную ячейку).")]
        [Range(0f, 1f)]
        [SerializeField] float sfxRandomChance = 0.25f;

        [Tooltip("Интервал тика планировщика (сек). Как часто ячейки тянут жребий.")]
        [SerializeField] float sfxRandomTickInterval = 2.5f;

        [Tooltip("Кулдаун ячейки после выстрела (сек). Не даёт одной и той же ячейке стрелять подряд.")]
        [SerializeField] float sfxRandomZoneCooldown = 15f;

        [Tooltip("Разброс кулдауна (0–1). Разводит ячейки друг относительно друга, чтобы не было залпа в один момент.")]
        [Range(0f, 1f)]
        [SerializeField] float sfxRandomCooldownJitter = 0.5f;

        [Tooltip("Сколько таких звуков может играть одновременно во всём мире.")]
        [SerializeField] int sfxRandomMaxConcurrent = 4;

        [Tooltip("Радиус разброса точки звука вокруг постройки (м).")]
        [SerializeField] float sfxRandomJitterRadius = 1.5f;

        [Tooltip("Запас к потолку жизни инстанса сверх длины ивента (сек). На длительность НЕ влияет — звук доигрывает сам, это только страховка от утечки.")]
        [SerializeField] float sfxRandomLifetimeMargin = 2f;

        [Header("Debug Gizmos")]
        [Tooltip("Толщина контура ячейки в пикселях (Handles: URP игнорирует Gizmos.lineWidth, поэтому толщина только через Handles).")]
        [SerializeField] float gizmoLineWidth = 4f;

        [Tooltip("Отступ оболочки ячейки наружу от построек (м). Оболочка = округлая оболочка кругов вокруг построек ячейки.")]
        [SerializeField] float gizmoHullPadding = 1.6f;

        [Tooltip("Рисовать пунктирный круг радиуса привязки вокруг центроида ячейки.")]
        [SerializeField] bool showAttachRadius = true;

        [Header("Debug")]
        [Tooltip("Логировать вход/выход ячеек и привязку построек в консоль.")]
        [SerializeField] bool logZoneActivity = false;

        [Tooltip("Писать в гизмо ячеек текст: состав, состояние, RTPC (sent/readback).")]
        [SerializeField] bool showZoneLabels = true;

        [Tooltip("Логировать выстрелы случайных городских звуков.")]
        [SerializeField] bool logSfxRandom = false;

        /// <summary>Сколько построек вмещает ячейка.</summary>
        public int CellCapacity => cellCapacity > 0 ? cellCapacity : 1;

        /// <summary>Радиус привязки новой постройки к существующей ячейке (м).</summary>
        public float AttachRadius => attachRadius > 0f ? attachRadius : 1f;

        public EventReference EventReference => eventReference;
        public string EventPath => eventPath;
        public string AudioBusPath => audioBusPath;
        public string DistanceRtpcName => distanceRtpcName;

        /// <summary>Диапазон нормализации Distance RTPC (м).</summary>
        public float DistanceRtpcRange => distanceRtpcRange > 0f ? distanceRtpcRange : 1f;

        public float ZoneDeathDistance => zoneDeathDistance;
        public float ZoneDeathHysteresis => zoneDeathHysteresis;
        public bool LogZoneActivity => logZoneActivity;
        public bool ShowZoneLabels => showZoneLabels;
        public bool LogSfxRandom => logSfxRandom;

        public float GizmoLineWidth => gizmoLineWidth > 0f ? gizmoLineWidth : 1f;
        public float GizmoHullPadding => gizmoHullPadding > 0f ? gizmoHullPadding : 0.5f;
        public bool ShowAttachRadius => showAttachRadius;

        public EventReference SfxRandomEventReference => sfxRandomEventReference;
        public string SfxRandomEventPath => sfxRandomEventPath;
        public float SfxRandomChance => sfxRandomChance;
        public float SfxRandomTickInterval => sfxRandomTickInterval > 0f ? sfxRandomTickInterval : 1f;
        public float SfxRandomZoneCooldown => sfxRandomZoneCooldown;
        public float SfxRandomCooldownJitter => Mathf.Clamp01(sfxRandomCooldownJitter);
        public int SfxRandomMaxConcurrent => sfxRandomMaxConcurrent > 0 ? sfxRandomMaxConcurrent : 1;
        public float SfxRandomJitterRadius => sfxRandomJitterRadius;
        public float SfxRandomLifetimeMargin => Mathf.Max(0f, sfxRandomLifetimeMargin);
    }
}
