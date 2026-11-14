using System.Reflection;
using TheyWillDescend.Presentation.Cameras;
using UnityEngine;

namespace TheyWillDescend.Presentation.Environment
{
    /// <summary>Shows volumetric fog in close gameplay and the pause menu.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuFogHeightController : MonoBehaviour
    {
        [SerializeField, Tooltip("Pure Volumetric Fog component to control.")]
        private MonoBehaviour fogVolume;

        [SerializeField] private PauseCameraSwitch pauseCamera;
        [SerializeField] private RTSCameraController cameraController;

        [SerializeField, Range(0f, .3f), Tooltip("Плотность при полной видимости. Можно менять в Play Mode; переходы зума/меню применяются как множитель.")]
        private float baseDensity = .003f;
        [SerializeField, Min(1f)] private float gameplayFogHeight = 65f;
        [SerializeField, Min(1f)] private float menuFogHeight = 35f;

        [Header("Gameplay zoom visibility")]
        [SerializeField, Min(0f), Tooltip("Полная плотность на ближайшем радиусе RTS-зума.")]
        private float fullFogZoomRadius = 5.1f;
        [SerializeField, Min(0f), Tooltip("На этом радиусе и дальше объёмный туман полностью выключен. Меню имеет приоритет.")]
        private float fogOffZoomRadius = 6.5f;

        [SerializeField, Min(0f), Tooltip("Время мягкого появления тумана при приближении, в секундах.")]
        private float zoomFadeInTime = 1.2f;
        [SerializeField, Min(0f), Tooltip("Время затухания тумана при отдалении, в секундах.")]
        private float zoomFadeOutTime = .7f;

        [Header("Plateau fog mask")]
        [SerializeField] private Shader plateauFogShader;
        [SerializeField, Tooltip("Регулирует плотность объёмного тумана по расстоянию от центра плато в плоскости XZ.")]
        private bool usePlateauMask;
        [SerializeField] private Vector2 plateauCenterXZ;
        [SerializeField, Min(0f), Tooltip("До этого радиуса действует плотность тумана на плато.")]
        private float plateauStartRadius = 120f;
        [SerializeField, Min(0f), Tooltip("На этом радиусе и дальше действует плотность тумана снаружи.")]
        private float plateauEndRadius = 220f;
        [SerializeField, Range(0f, 2f)] private float gameplayPlateauStrength;
        [SerializeField, Range(0f, 2f)] private float menuPlateauStrength;
        [SerializeField, Range(0f, 2f)] private float outsidePlateauStrength = 1f;

        private static readonly int PlateauMaskId = Shader.PropertyToID("_PlateauFogMask");
        private static readonly int PlateauStrengthId = Shader.PropertyToID("_PlateauFogStrength");
        private static readonly int CullModeId = Shader.PropertyToID("_CullMode");

        private FieldInfo _heightField;
        private FieldInfo _densityField;
        private Material[] _volumeMaterials;
        private MethodInfo _updateMaterial;
        private Shader _originalFogShader;
        private MonoBehaviour _boundVolume;
        private float _originalHeight;
        private float _originalDensity;
        private float _lastAppliedHeight;
        private float _lastAppliedDensity;
        private bool _originalEnabled;
        private bool _hasBinding;
        private float _gameplayVisibility;
        private bool _visibilityInitialized;

        private void OnEnable()
        {
            // Wait until LateUpdate so the RTS camera has initialized its zoom in Start.
            if (Application.isPlaying)
                BindVolume();
        }

        private bool BindVolume()
        {
            if (_hasBinding)
                return _boundVolume != null;

            // The optional package exposes no height setter. Cache its serialized field once;
            // keep vendor source and the Presentation assembly's dependencies unchanged.
            if (fogVolume == null || fogVolume.GetType().FullName != "BKPureNature.PureVolumetricFog")
            {
                Debug.LogWarning("Menu fog height requires a Pure Volumetric Fog reference.", this);
                return false;
            }

            _heightField = fogVolume.GetType().GetField("groundFogHeight",
                BindingFlags.Instance | BindingFlags.NonPublic);
            _densityField = fogVolume.GetType().GetField("density",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_heightField == null || _heightField.FieldType != typeof(float)
                || _densityField == null || _densityField.FieldType != typeof(float))
            {
                Debug.LogWarning("Pure Volumetric Fog height API changed; menu height control is unavailable.", this);
                return false;
            }

            _boundVolume = fogVolume;
            const BindingFlags volumeMembers = BindingFlags.Instance | BindingFlags.NonPublic;
            _volumeMaterials = fogVolume.GetType().GetField("_materials", volumeMembers)?.GetValue(fogVolume) as Material[];
            _updateMaterial = fogVolume.GetType().GetMethod("UpdateMaterial", volumeMembers);
            if (plateauFogShader != null && (_volumeMaterials == null || _updateMaterial == null))
                Debug.LogWarning("Pure Volumetric Fog material API changed; plateau fog masking is unavailable.", this);

            _originalHeight = (float)_heightField.GetValue(_boundVolume);
            _originalDensity = (float)_densityField.GetValue(_boundVolume);
            _originalEnabled = _boundVolume.enabled;
            _lastAppliedHeight = _originalHeight;
            _lastAppliedDensity = _originalDensity;
            _hasBinding = true;
            return true;
        }

        private void LateUpdate()
        {
            if (_hasBinding && _boundVolume != null)
                ApplyFogForView(cameraController != null ? cameraController.CurrentZoomRadius : fullFogZoomRadius,
                    pauseCamera != null ? pauseCamera.MenuBlendProgress : 0f, Time.unscaledDeltaTime);
        }

        private void ApplyFogForView(float zoomRadius, float menuBlend, float deltaTime)
        {
            if (!_hasBinding || _boundVolume == null)
                return;

            ApplyHeightForView(menuBlend);
            float end = Mathf.Max(fullFogZoomRadius + .01f, fogOffZoomRadius);
            float closeWeight = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(fullFogZoomRadius, end, zoomRadius));
            if (!_visibilityInitialized)
            {
                _gameplayVisibility = closeWeight;
                _visibilityInitialized = true;
            }

            // RTS zoom crosses the visibility band in a fraction of a second.
            // Rate-limit its contribution independently of camera speed and frame rate;
            // keep the menu's own Cinemachine blend unchanged.
            float fadeTime = closeWeight > _gameplayVisibility ? zoomFadeInTime : zoomFadeOutTime;
            _gameplayVisibility = fadeTime <= 0f ? closeWeight : Mathf.MoveTowards(
                _gameplayVisibility, closeWeight, Mathf.Max(0f, deltaTime) / fadeTime);
            float gameplayVisibility = Mathf.SmoothStep(0f, 1f, _gameplayVisibility);
            float visibility = Mathf.Lerp(gameplayVisibility, 1f, Mathf.Clamp01(menuBlend));
            float fullDensity = Mathf.Clamp(baseDensity, 0f, .3f);
            bool showFog = _originalEnabled && visibility > .0001f && fullDensity > 0f;
            float density = showFog ? fullDensity * visibility : 0f;
            if (!Mathf.Approximately(_lastAppliedDensity, density))
            {
                _densityField.SetValue(_boundVolume, density);
                _lastAppliedDensity = density;
            }

            // No active volume means the package skips enqueuing its raymarch pass.
            // Re-enable only on a visibility transition, after setting height/density.
            if (_boundVolume.enabled != showFog)
                _boundVolume.enabled = showFog;
            if (showFog)
                ApplyPlateauMask(menuBlend);
        }

        private void ApplyPlateauMask(float menuBlend)
        {
            if (_volumeMaterials == null || _updateMaterial == null)
                return;

            if (!usePlateauMask || plateauFogShader == null || !plateauFogShader.isSupported)
            {
                RestoreFogShader();
                return;
            }

            float start = Mathf.Max(0f, plateauStartRadius);
            float end = Mathf.Max(start + .01f, plateauEndRadius);
            float inside = Mathf.Lerp(gameplayPlateauStrength, menuPlateauStrength, Mathf.Clamp01(menuBlend));
            var mask = new Vector4(plateauCenterXZ.x, plateauCenterXZ.y, start, end);
            var strengths = new Vector4(inside, outsidePlateauStrength, 0f, 0f);
            bool shaderChanged = false;
            foreach (var material in _volumeMaterials)
            {
                if (material == null) continue;
                if (material.shader != plateauFogShader)
                {
                    _originalFogShader = material.shader;
                    SetFogShader(material, plateauFogShader);
                    shaderChanged = true;
                }
                material.SetVector(PlateauMaskId, mask);
                material.SetVector(PlateauStrengthId, strengths);
            }

            if (shaderChanged)
                _updateMaterial.Invoke(_boundVolume, null);
        }

        private void RestoreFogShader()
        {
            if (_originalFogShader == null) return;

            bool shaderChanged = false;
            foreach (var material in _volumeMaterials)
            {
                if (material == null || material.shader == _originalFogShader) continue;
                SetFogShader(material, _originalFogShader);
                shaderChanged = true;
            }

            if (shaderChanged)
                _updateMaterial.Invoke(_boundVolume, null);
            _originalFogShader = null;
        }

        private static void SetFogShader(Material material, Shader shader)
        {
            float cullMode = material.GetFloat(CullModeId);
            material.shader = shader;
            material.SetFloat(CullModeId, cullMode);
        }

        private void ApplyHeightForView(float menuBlend)
        {
            if (!_hasBinding || _boundVolume == null)
                return;

            float height = Mathf.Lerp(Mathf.Max(1f, gameplayFogHeight),
                Mathf.Max(1f, menuFogHeight), Mathf.Clamp01(menuBlend));
            // Only box/write while the value actually changes. No terrain/noise rebuild:
            // the package uploads this field to its material in its normal frame update.
            if (!Mathf.Approximately(_lastAppliedHeight, height))
            {
                _heightField.SetValue(_boundVolume, height);
                _lastAppliedHeight = height;
            }
        }

        private void OnDisable()
        {
            if (_hasBinding && _boundVolume != null)
            {
                _heightField.SetValue(_boundVolume, _originalHeight);
                _densityField.SetValue(_boundVolume, _originalDensity);
                RestoreFogShader();
                _boundVolume.enabled = _originalEnabled;
            }

            _hasBinding = false;
            _boundVolume = null;
            _heightField = null;
            _densityField = null;
            _volumeMaterials = null;
            _updateMaterial = null;
            _originalFogShader = null;
            _visibilityInitialized = false;
            _gameplayVisibility = 0f;
        }
    }
}
