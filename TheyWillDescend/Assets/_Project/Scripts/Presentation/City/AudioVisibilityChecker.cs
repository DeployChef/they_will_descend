using System.Collections.Generic;
using UnityEngine;
using TheyWillDescend.Presentation.Audio;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Проверка видимости аудио-зон. Определяет, какие зоны находятся
    /// в конусе обзора камеры + в радиусе видимости.
    /// </summary>
    public sealed class AudioVisibilityChecker : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] Camera mainCamera;

        [Header("Settings")]
        [SerializeField] AudioZoneSettings settings;

        /// <summary>Позиция камеры на последнем тике.</summary>
        private Vector3 _lastCameraPosition;

        /// <summary>Направление камеры на последнем тике.</summary>
        private Vector3 _lastCameraForward;

        /// <summary>Половина ГОРИзонтального угла обзора в радианах.</summary>
        private float _halfHorizontalFOVRadians;

        /// <summary>Квадрат максимальной дистанции.</summary>
        private float _maxDistanceSquared;

        /// <summary>Список видимых зон.</summary>
        private readonly List<AudioZone> _visibleZones = new();

        /// <summary>Список невидимых зон.</summary>
        private readonly List<AudioZone> _hiddenZones = new();

        public Camera Camera => mainCamera;
        public AudioZoneSettings Settings => settings;

        void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera != null)
            {
                _halfHorizontalFOVRadians = HalfHorizontalFov(mainCamera);
                _maxDistanceSquared = settings.MaxDistance * settings.MaxDistance;
            }
        }

        /// <summary>
        /// Горизонтальный FOV из вертикального (fieldOfView в Unity — вертикальный).
        /// Горизонтальный угол всегда шире, поэтому зоны раньше глохли у центра.
        /// </summary>
        static float HalfHorizontalFov(Camera cam)
        {
            var vFovRad = cam.fieldOfView * Mathf.Deg2Rad * 0.5f;
            var hFovRad = 2f * Mathf.Atan(Mathf.Tan(vFovRad) * cam.aspect);
            return hFovRad * 0.5f;
        }

        /// <summary>
        /// Обновляет видимость всех зон.
        /// </summary>
        public void UpdateVisibility(AudioZone[] allZones)
        {
            // Камера может появиться позже Bootstrap (Game-сцена additive) — ищем лениво.
            if (mainCamera == null)
                mainCamera = Camera.main;
            if (mainCamera == null || settings == null)
                return;

            var cameraPos = mainCamera.transform.position;
            var cameraForward = mainCamera.transform.forward;

            if (cameraPos != _lastCameraPosition || cameraForward != _lastCameraForward)
            {
                _lastCameraPosition = cameraPos;
                _lastCameraForward = cameraForward;

                _halfHorizontalFOVRadians = HalfHorizontalFov(mainCamera);
                _maxDistanceSquared = settings.MaxDistance * settings.MaxDistance;
            }

            // Выравниваем forward по горизонтали: камера смотрит вниз,
            // из-за наклона конус обзора сужался и зоны глохли у центра.
            var flatForward = cameraForward;
            flatForward.y = 0f;
            flatForward.Normalize();

            _visibleZones.Clear();
            _hiddenZones.Clear();

            for (var i = 0; i < allZones.Length; i++)
            {
                var zone = allZones[i];
                if (zone == null)
                    continue;

                var toZone = zone.WorldPosition - cameraPos;
                var distSquared = toZone.sqrMagnitude;

                // Проверяем дистанцию.
                if (distSquared > _maxDistanceSquared)
                {
                    _hiddenZones.Add(zone);
                    continue;
                }

                // Проверяем угол (всё в горизонтальной плоскости).
                toZone.y = 0f;
                toZone.Normalize();
                var dot = Vector3.Dot(flatForward, toZone);
                var cosHalfFov = Mathf.Cos(_halfHorizontalFOVRadians);

                if (dot > cosHalfFov)
                {
                    _visibleZones.Add(zone);
                }
                else
                {
                    _hiddenZones.Add(zone);
                }
            }
        }

        /// <summary>
        /// Применяет результаты проверки видимости к зонам.
        /// </summary>
        public void ApplyVisibility()
        {
            for (var i = 0; i < _visibleZones.Count; i++)
            {
                var zone = _visibleZones[i];
                if (zone != null && !zone.IsVisible)
                    zone.SetActive(true);
            }

            for (var i = 0; i < _hiddenZones.Count; i++)
            {
                var zone = _hiddenZones[i];
                if (zone != null && zone.IsVisible)
                    zone.SetActive(false);
            }
        }
    }
}
