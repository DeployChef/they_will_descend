using Unity.Cinemachine;
using UnityEngine;

namespace TheyWillDescend.Presentation.Cameras
{
    /// <summary>
    /// Плавно вращает виртуальную камеру меню вокруг Pivot, пока открыто меню паузы.
    /// Поза камеры между открытиями сохраняется: следующее открытие начинается там,
    /// где меню закрыли. Возврат к исходной позе — только по Reset Pose On Open.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class PauseCameraOrbit : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField, Tooltip("Источник состояния меню. Если пусто — берётся PauseCameraSwitch.Current.")]
        PauseCameraSwitch cameraSwitch;

        [Header("Orbit")]
        [SerializeField, Tooltip("Вокруг чего вращать. Если пусто — используется цель виртуальной камеры (LookAt, иначе Follow).")]
        Transform pivot;
        [SerializeField, Tooltip("Ось вращения в мировых координатах. Vector3.up — горизонтальная орбита.")]
        Vector3 axis = Vector3.up;
        [SerializeField, Tooltip("Градусов в секунду при полностью приехавшей камере. Знак меняет направление.")]
        float degreesPerSecond = 8f;
        [SerializeField, Tooltip("Разгонять вращение вместе с переходом камеры, чтобы старт был плавным.")]
        bool rampWithBlend = true;
        [SerializeField, Tooltip("Тикать время перехода, а не игры: меню работает на остановленной симуляции.")]
        bool useUnscaledTime = true;

        [Header("Pose")]
        [SerializeField, Tooltip("Возвращать камеру в исходную позу при закрытом меню. Выключено — поза сохраняется между открытиями.")]
        bool resetPoseOnOpen = false;

        CinemachineCamera _camera;
        Vector3 _basePosition;
        Quaternion _baseRotation;
        bool _warnedAboutPivot;

        PauseCameraSwitch Switch => cameraSwitch != null ? cameraSwitch : PauseCameraSwitch.Current;

        void Awake()
        {
            _camera = GetComponent<CinemachineCamera>();
            _basePosition = transform.position;
            _baseRotation = transform.rotation;
        }

        void OnEnable()
        {
            // Объект включают в момент Engage, Update в этом кадре может не успеть.
            if (resetPoseOnOpen)
                RestoreBasePose();
        }

        void Update()
        {
            var cameraState = Switch;
            if (!IsThisMenuCamera(cameraState) || !cameraState.IsEngaged)
            {
                if (resetPoseOnOpen)
                    RestoreBasePose();
                return;
            }

            var orbitPivot = ResolvePivot();
            if (orbitPivot == null)
                return;

            if (axis.sqrMagnitude < 0.0001f)
                return;

            float speed = degreesPerSecond;
            if (rampWithBlend)
                speed *= cameraState.MenuBlendProgress;

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            transform.RotateAround(orbitPivot.position, axis.normalized, speed * deltaTime);
        }

        /// <summary>Орбита имеет смысл только для той камеры, которую показывает меню.</summary>
        bool IsThisMenuCamera(PauseCameraSwitch cameraState)
            => cameraState != null && cameraState.MenuCamera == _camera;

        Transform ResolvePivot()
        {
            if (pivot != null)
                return pivot;

            if (_camera != null)
            {
                if (_camera.Target.LookAtTarget != null)
                    return _camera.Target.LookAtTarget;
                if (_camera.Target.TrackingTarget != null)
                    return _camera.Target.TrackingTarget;
            }

            if (!_warnedAboutPivot)
            {
                _warnedAboutPivot = true;
                Debug.LogWarning("PauseCameraOrbit: pivot is not set and the virtual camera has no target, orbit is disabled.", this);
            }
            return null;
        }

        void RestoreBasePose()
        {
            if (transform.position == _basePosition && transform.rotation == _baseRotation)
                return;
            transform.SetPositionAndRotation(_basePosition, _baseRotation);
        }
    }
}
