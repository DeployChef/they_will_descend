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

        /// <summary>Ступень дальнего зума на последнем тике (она тоже меняет слышимость).</summary>
        private bool _lastFullyZoomedOut;

        /// <summary>
        /// Косинус половины ГОРИЗОНТАЛЬНОГО угла обзора. Хранится косинус, а не
        /// угол: сравнение с ним и есть проверка попадания в конус, пересчёт —
        /// только на смене ракурса камеры.
        /// </summary>
        private float _cosHalfFov;

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
                _cosHalfFov = Mathf.Cos(HalfHorizontalFov(mainCamera));
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
        /// Сдвинулась ли камера с тех пор, как видимость считалась в последний раз:
        /// позиция, направление взгляда или ступень дальнего зума. Дешёвая проверка
        /// без аллокаций — по ней менеджер пропускает полный рескан ячеек, когда
        /// камера стоит на месте.
        /// </summary>
        public bool CameraMoved()
        {
            if (mainCamera == null)
                return true;

            var t = mainCamera.transform;
            if (t.position != _lastCameraPosition || t.forward != _lastCameraForward)
                return true;

            return IsFullyZoomedOut() != _lastFullyZoomedOut;
        }

        /// <summary>
        /// Камера на самом дальнем шаге зума. Тогда локальные инстансы ячеек не
        /// играют вообще — город слышен общим амбиентом.
        /// </summary>
        bool IsFullyZoomedOut()
        {
            // Контроллер в additive-сцене может появиться позже — ищем лениво, один раз.
            if (_rtsController == null && !_lookedForController)
            {
                _lookedForController = true;
                _rtsController = FindFirstObjectByType<RTSCameraController>();
            }

            return _rtsController != null && _rtsController.IsFullyZoomedOut;
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

                _cosHalfFov = Mathf.Cos(HalfHorizontalFov(mainCamera));
            }

            _visibleCells.Clear();
            _hiddenCells.Clear();

            // Самый дальний шаг зума — локальные инстансы ячеек не играют вообще.
            var fullyZoomedOut = IsFullyZoomedOut();
            _lastFullyZoomedOut = fullyZoomedOut;

            if (fullyZoomedOut)
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

            // Ближний радиус: ячейки в нём не умирают за конусом — камера над
            // ячейкой (HQ-зона LOD), инстанс живёт постоянно, приглушённым.
            // Приглушение за спиной камеры делает FMOD (Event Orientation).
            var nearRadius = settings.NearKeepAliveRadius;
            // 0 = фича выключена: -1 не матчится ни с одной дистанцией.
            var nearRadiusSq = nearRadius > 0f ? nearRadius * nearRadius : -1f;

            for (var i = 0; i < allCells.Count; i++)
            {
                var cell = allCells[i];
                if (cell == null)
                    continue;

                // Только угол: дистанция НЕ ограничивает слышимость —
                // дальние ячейки в кадре звучат так же, как ближние.
                var toCell = cell.WorldPosition - cameraPos;
                toCell.y = 0f;
                var distSq = toCell.sqrMagnitude;
                toCell.Normalize();
                var dot = Vector3.Dot(flatForward, toCell);

                // В конусе — живая. Вне конуса, но в ближнем радиусе — тоже живая
                // (приглушённая). Умирают только дальние вне конуса.
                if (dot > _cosHalfFov || distSq <= nearRadiusSq)
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
