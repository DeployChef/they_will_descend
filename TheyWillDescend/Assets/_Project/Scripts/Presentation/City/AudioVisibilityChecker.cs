using System.Collections.Generic;
using UnityEngine;
using TheyWillDescend.Presentation.Audio;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Проверка видимости аудио-ячеек. Ячейка слышна, если её центроид попал в
    /// ГОРИЗОНТАЛЬНЫЙ конус обзора камеры — без дистанционного ограничения (дальние
    /// ячейки наравне с ближними, пока в кадре). Исключение: на самом дальнем шаге
    /// зума камеры все ячейки глушатся — локальные инстансы не играют.
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

        /// <summary>Половина ГОРИЗОНТАЛЬНОГО угла обзора в радианах.</summary>
        private float _halfHorizontalFOVRadians;

        /// <summary>Контроллер RTS-камеры (ленивый поиск): факт максимального отдаления.</summary>
        private RTSCameraController _rtsController;

        /// <summary>Попытались ли найти контроллер камеры.</summary>
        private bool _lookedForController;

        /// <summary>Список видимых ячеек.</summary>
        private readonly List<AudioCell> _visibleCells = new();

        /// <summary>Список невидимых ячеек.</summary>
        private readonly List<AudioCell> _hiddenCells = new();

        public Camera Camera => mainCamera;
        public AudioZoneSettings Settings => settings;

        void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera != null)
            {
                _halfHorizontalFOVRadians = HalfHorizontalFov(mainCamera);
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
        /// Обновляет видимость всех ячеек.
        /// </summary>
        public void UpdateVisibility(IReadOnlyList<AudioCell> allCells)
        {
            // Камера может появиться позже Bootstrap (Game-сцена additive) — ищем лениво.
            if (mainCamera == null)
                mainCamera = Camera.main;
            if (mainCamera == null || settings == null || allCells == null)
                return;

            var cameraPos = mainCamera.transform.position;
            var cameraForward = mainCamera.transform.forward;

            if (cameraPos != _lastCameraPosition || cameraForward != _lastCameraForward)
            {
                _lastCameraPosition = cameraPos;
                _lastCameraForward = cameraForward;

                _halfHorizontalFOVRadians = HalfHorizontalFov(mainCamera);
            }

            _visibleCells.Clear();
            _hiddenCells.Clear();

            // Ленивый поиск контроллера камеры (он в additive-сцене может появиться позже).
            if (_rtsController == null && !_lookedForController)
            {
                _lookedForController = true;
                _rtsController = FindFirstObjectByType<RTSCameraController>();
            }

            // Самый дальний шаг зума — локальные инстансы ячеек не играют вообще.
            if (_rtsController != null && _rtsController.IsFullyZoomedOut)
            {
                for (var i = 0; i < allCells.Count; i++)
                {
                    if (allCells[i] != null)
                        _hiddenCells.Add(allCells[i]);
                }
                return;
            }

            // Выравниваем forward по горизонтали: камера смотрит вниз,
            // из-за наклона конус обзора сужался и ячейки глохли у центра.
            var flatForward = cameraForward;
            flatForward.y = 0f;
            flatForward.Normalize();

            for (var i = 0; i < allCells.Count; i++)
            {
                var cell = allCells[i];
                if (cell == null)
                    continue;

                // Только угол: дистанция НЕ ограничивает слышимость —
                // дальние ячейки в кадре звучат так же, как ближние.
                var toCell = cell.WorldPosition - cameraPos;
                toCell.y = 0f;
                toCell.Normalize();
                var dot = Vector3.Dot(flatForward, toCell);
                var cosHalfFov = Mathf.Cos(_halfHorizontalFOVRadians);

                if (dot > cosHalfFov)
                {
                    _visibleCells.Add(cell);
                }
                else
                {
                    _hiddenCells.Add(cell);
                }
            }
        }

        /// <summary>
        /// Применяет результаты проверки видимости к ячейкам.
        /// </summary>
        public void ApplyVisibility()
        {
            for (var i = 0; i < _visibleCells.Count; i++)
            {
                var cell = _visibleCells[i];
                if (cell != null && !cell.IsVisible)
                    cell.SetActive(true);
            }

            for (var i = 0; i < _hiddenCells.Count; i++)
            {
                var cell = _hiddenCells[i];
                if (cell != null && cell.IsVisible)
                    cell.SetActive(false);
            }
        }
    }
}
