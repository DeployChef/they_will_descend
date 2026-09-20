using Unity.Cinemachine;
using UnityEngine;

namespace TheyWillDescend.Presentation.Cameras
{
    /// <summary>
    /// Переключает Cinemachine на отдельную виртуальную камеру, пока открыто меню паузы,
    /// и возвращает обратно при закрытии. Скорость перехода настраивается в инспекторе,
    /// камера передаётся через поле menuCamera.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    public sealed class PauseCameraSwitch : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField, Tooltip("Виртуальная камера меню. Активируется, пока меню открыто.")]
        CinemachineCamera menuCamera;

        [Header("Transition")]
        [SerializeField, Tooltip("Форма кривой перехода.")]
        CinemachineBlendDefinition.Styles blendStyle = CinemachineBlendDefinition.Styles.EaseInOut;

        [SerializeField, Min(0f), Tooltip("Сколько секунд длится переход НА камеру меню.")]
        float enterTime = 0.5f;

        [SerializeField, Min(0f), Tooltip("Сколько секунд длится переход ОБРАТНО на игровую камеру.")]
        float exitTime = 0.4f;

        [SerializeField, Tooltip("Приоритет камеры меню, пока меню открыто. Должен быть выше игрового (у VCam_Gameplay = 10).")]
        int menuPriority = 100;

        [Header("Optional")]
        [SerializeField, Tooltip("Полностью выключать объект камеры меню, когда меню закрыто.")]
        bool autoDisableMenuCamera = true;

        [SerializeField, Tooltip("Эти компоненты отключаются на время открытого меню (например RTSCameraController).")]
        Behaviour[] disabledWhileMenuOpen;

        /// <summary>Экземпляр в сцене. Ставится в Awake, поэтому объект должен быть активен на старте.</summary>
        public static PauseCameraSwitch Current { get; private set; }

        public bool IsEngaged { get; private set; }

        /// <summary>
        /// Прогресс перехода на камеру меню: 1 — камера полностью на месте меню, 0 — на игровой.
        /// Во время перехода считается по весу текущего бленда, вне перехода — целевое состояние.
        /// Если мозга или камеры нет, сразу возвращается целевое значение, чтобы UI не ждал вечно.
        /// </summary>
        public float MenuBlendProgress
        {
            get
            {
                float target = IsEngaged ? 1f : 0f;
                if (menuCamera == null)
                    return target;

                var brain = _brain != null && _brain.isActiveAndEnabled ? _brain : null;
                if (brain == null)
                    return target;

                if (TryGetBlendWeight(brain.ActiveBlend, menuCamera, out float weight))
                    return weight;

                return brain.IsLiveChild(menuCamera) ? 1f : 0f;
            }
        }

        CinemachineBrain _brain;
        PrioritySettings _menuCameraOriginalPriority;
        bool _menuCameraWasActive;
        bool _initialized;

        void Awake()
        {
            Current = this;
            EnsureInitialized();
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        void EnsureInitialized()
        {
            if (_initialized || menuCamera == null)
                return;

            _initialized = true;
            _menuCameraOriginalPriority = menuCamera.Priority;
            _menuCameraWasActive = menuCamera.gameObject.activeSelf;

            if (autoDisableMenuCamera)
                menuCamera.gameObject.SetActive(false);
        }

        /// <summary>Меню открыли — уходим на камеру меню.</summary>
        public void Engage()
        {
            if (IsEngaged || menuCamera == null)
                return;

            EnsureInitialized();

            var brain = ResolveBrain();
            if (brain == null)
                return;

            IsEngaged = true;

            // Мозг читает DefaultBlend в момент создания бленда, поэтому его достаточно
            // выставить прямо перед сменой приоритета.
            brain.DefaultBlend = new CinemachineBlendDefinition(blendStyle, enterTime);

            SetOptionalBehaviours(false);

            menuCamera.gameObject.SetActive(true);
            menuCamera.Priority = menuPriority;
            menuCamera.Prioritize();
        }

        /// <summary>Меню закрыли — возвращаемся на игровую камеру.</summary>
        public void Release()
        {
            if (!IsEngaged)
                return;

            IsEngaged = false;

            var brain = ResolveBrain();
            if (brain != null)
                brain.DefaultBlend = new CinemachineBlendDefinition(blendStyle, exitTime);

            if (menuCamera != null)
            {
                menuCamera.Priority = _menuCameraOriginalPriority;
                if (autoDisableMenuCamera || !_menuCameraWasActive)
                    menuCamera.gameObject.SetActive(false);
            }

            SetOptionalBehaviours(true);
        }

        CinemachineBrain ResolveBrain()
        {
            if (_brain != null && _brain.isActiveAndEnabled)
                return _brain;

            _brain = CinemachineBrain.ActiveBrainCount > 0 ? CinemachineBrain.GetActiveBrain(0) : null;
            if (_brain == null)
                _brain = FindAnyObjectByType<CinemachineBrain>();

            if (_brain == null)
                Debug.LogError("PauseCameraSwitch: CinemachineBrain not found on any camera.", this);

            return _brain;
        }

        void SetOptionalBehaviours(bool enabled)
        {
            if (disabledWhileMenuOpen == null)
                return;

            for (int i = 0; i < disabledWhileMenuOpen.Length; i++)
            {
                var behaviour = disabledWhileMenuOpen[i];
                if (behaviour != null)
                    behaviour.enabled = enabled;
            }
        }

        /// <summary>
        /// Вес камеры внутри текущего бленда (0 — камера A, 1 — камера B). Рекурсивно
        /// заходит во вложенные бленды, потому что вложенный источник приходит в виде NestedBlendSource.
        /// </summary>
        static bool TryGetBlendWeight(CinemachineBlend blend, ICinemachineCamera cam, out float weight)
        {
            weight = 0f;
            if (blend == null || !blend.IsValid || cam == null)
                return false;

            float value = Mathf.Clamp01(blend.BlendWeight);
            if (blend.CamA == cam)
            {
                weight = 1f - value;
                return true;
            }

            if (blend.CamB == cam)
            {
                weight = value;
                return true;
            }

            if (blend.CamA is NestedBlendSource nestedA && TryGetBlendWeight(nestedA.Blend, cam, out weight))
                return true;

            return blend.CamB is NestedBlendSource nestedB && TryGetBlendWeight(nestedB.Blend, cam, out weight);
        }
    }
}
