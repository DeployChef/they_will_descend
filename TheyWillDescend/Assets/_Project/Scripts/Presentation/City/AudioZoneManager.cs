using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Entities;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Infrastructure.Logging;
using UnityEngine;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Presentation.City;namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Главный менеджер аудио-зон. 120 зон вместо 14 592 ячеек.
    /// 12 угловых секторов × 10 дистанционных зон = 120 зон.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioZoneManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] AudioZoneSettings settings;
        [SerializeField] AudioVisibilityChecker visibilityChecker;

        [Header("FMOD Banks")]
        [Tooltip("Банки с ивентом ambience_town (без расширения .bank). Master грузится всегда.")]
        [SerializeField] string[] fmodBanks = { "AudioCity" };

        /// <summary>Плоский список всех зон (120 штук).</summary>
        private AudioZone[] _zones;

        /// <summary>Центр сетки (из DOTS).</summary>
        private float3 _gridCenter;

        /// <summary>Нужно ли пересоздать сетку.</summary>
        private bool _needsRebuild;

        /// <summary>Счётчик кадров между тиками видимости.</summary>
        private int _frameCounter;

        /// <summary>Таймер fallback-построения зон, если DOTS-сетка не готова.</summary>
        private float _fallbackTimer;

        public AudioZoneSettings Settings => settings;
        public AudioVisibilityChecker VisibilityChecker => visibilityChecker;
        public AudioZone[] Zones => _zones;

        void Awake()
        {
            if (visibilityChecker == null)
                visibilityChecker = GetComponentInChildren<AudioVisibilityChecker>();

            if (settings == null)
            {
                GameLog.Error("AudioZoneManager: AudioZoneSettings is not assigned.");
                enabled = false;
                return;
            }
        }

        void OnEnable()
        {
            _needsRebuild = true;
        }

        void Update()
        {
            if (!enabled || settings == null)
                return;

            // Пытаемся получить данные из DOTS.
            var gridReady = false;
            if (SimWorld.TryGet(out var em, out var bag) && em.HasComponent<CityGrid>(bag))
            {
                var grid = em.GetComponentData<CityGrid>(bag);
                if (grid.Ready != 0 && grid.Config.IsValid)
                {
                    gridReady = true;
                    _fallbackTimer = 0f;
                    var newCenter = grid.Center;
                    if (_needsRebuild || !math.all(newCenter == _gridCenter))
                    {
                        _gridCenter = newCenter;
                        _needsRebuild = true;
                    }
                }
            }

            // Fallback: если DOTS-сетка не готова 3 секунды — строим зоны
            // с центром (0,0,0), чтобы гизмо и дебаг были видны сразу.
            if (!gridReady && _zones == null)
            {
                _fallbackTimer += Time.deltaTime;
                if (_fallbackTimer >= 3f)
                {
                    GameLog.Warning("AudioZoneManager: CityGrid not ready after 3s, building zones with center (0,0,0).");
                    _gridCenter = float3.zero;
                    _needsRebuild = true;
                }
            }

            if (_needsRebuild)
            {
                BuildZones();
                _needsRebuild = false;
            }

            // Тик видимости каждые 2 кадра.
            _frameCounter++;
            if (_frameCounter >= 2 && _zones != null && _zones.Length > 0)
            {
                _frameCounter = 0;
                UpdateVisibilityBatch();
            }
        }

        /// <summary>
        /// Создаёт 120 аудио-зон: 12 угловых × 10 дистанционных.
        /// </summary>
        private void BuildZones()
        {
            // Загружаем банки до создания инстансов зон.
            FmodBankLoader.LoadBanks(fmodBanks);

            DisposeZones();

            var totalZones = settings.AngularSectors * settings.RadialZones;
            _zones = new AudioZone[totalZones];

            for (var sector = 0; sector < settings.AngularSectors; sector++)
            {
                for (var radial = 0; radial < settings.RadialZones; radial++)
                {
                    var index = sector * settings.RadialZones + radial;
                    var zone = new AudioZone(sector, radial, settings);

                    // Средняя дистанция для этого дистанционного кольца
                    var distanceFromCenter = (radial + 0.5f) * settings.ZoneDepth;
                    zone.SetWorldPosition(_gridCenter, distanceFromCenter);

                    _zones[index] = zone;
                }
            }

            GameLog.Info($"AudioZoneManager: created {totalZones} zones (grid center: {_gridCenter}).");
        }

        /// <summary>
        /// Обновляет видимость зон батчами + Distance RTPC (audio LOD) активных зон.
        /// </summary>
        private void UpdateVisibilityBatch()
        {
            if (_zones == null || _zones.Length == 0)
                return;

            if (visibilityChecker != null)
            {
                visibilityChecker.UpdateVisibility(_zones);
                visibilityChecker.ApplyVisibility();
            }

            // Audio LOD + 3D-позиция активных зон: каждый тик.
            var cam = visibilityChecker != null ? visibilityChecker.Camera : Camera.main;
            if (cam != null)
            {
                var camPos = cam.transform.position;
                var rtpcName = settings.DistanceRtpcName;
                var maxDist = settings.MaxDistance;
                for (var i = 0; i < _zones.Length; i++)
                {
                    var zone = _zones[i];
                    if (zone == null || !zone.IsActive)
                        continue;

                    zone.UpdateDistanceRtpc(camPos, rtpcName, maxDist);
                    zone.Update3DAttributes();
                }
            }
        }

        /// <summary>
        /// Находит зону по позиции в мире (близжайшая).
        /// </summary>
        public AudioZone FindZoneNear(Vector3 worldPos)
        {
            if (_zones == null || _zones.Length == 0)
                return null;

            var bestZone = _zones[0];
            var bestDist = float.MaxValue;

            for (var i = 0; i < _zones.Length; i++)
            {
                var zone = _zones[i];
                var dist = (zone.WorldPosition - worldPos).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestZone = zone;
                }
            }

            return bestZone;
        }

        /// <summary>
        /// Освобождает все ресурсы.
        /// </summary>
        private void DisposeZones()
        {
            if (_zones != null)
            {
                for (var i = 0; i < _zones.Length; i++)
                {
                    _zones[i]?.Dispose();
                }
                _zones = null;
            }
        }

        void OnDestroy() => DisposeZones();

        // ===== Debug Gizmos =====

        /// <summary>Показывать гизмо зон (рисуются всегда, не только при выделении).</summary>
        [Header("Debug")]
        [SerializeField] bool showGizmos = true;

        void OnDrawGizmos()
        {
            if (!showGizmos || _zones == null || settings == null)
                return;

            for (var i = 0; i < _zones.Length; i++)
            {
                if (_zones[i] != null)
                    _zones[i].OnDrawGizmos(_gridCenter, settings);
            }
        }
    }
}
