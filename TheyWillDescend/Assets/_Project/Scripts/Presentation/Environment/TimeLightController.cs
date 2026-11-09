using UnityEngine;
using Unity.Entities;
using TheyWillDescend.Simulation.Time;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Presentation.Cameras;
using TheyWillDescend.Presentation.Rendering;

namespace TheyWillDescend.Presentation.Environment
{
    /// <summary>
    /// Связывает GameTime (ECS) с Directional Light и Skybox в Unity.
    /// Pull-паттерн: в LateUpdate читает ECS и применяет к Unity-объектам.
    /// Не пишет в ECS.
    /// </summary>
    public class TimeLightController : MonoBehaviour
    {
        [Header("Light Setup")]
        [SerializeField] private Light dayLight;

        [Header("Day/Night Curve (Intensity)")]
        [SerializeField] private AnimationCurve lightIntensityCurve;
        [SerializeField, Min(0f)] private float dayLightIntensity = 1f;
        [SerializeField, Min(0f)] private float nightLightIntensity = 0f;

        [Header("Ambient Lighting")]
        [SerializeField] private bool useAmbientLighting = true;
        [SerializeField] private Color dayAmbientColor = Color.white;
        [SerializeField] private Color nightAmbientColor = new Color(124f / 255f, 167f / 255f, 250f / 255f, 1f);

        [Header("Sun Angle")]
        [SerializeField] private float minSunAngle = -10f;
        [SerializeField] private float maxSunAngle = 110f;

        [Header("Color Presets")]
        [SerializeField] private Color nightColor = new Color(0.1f, 0.1f, 0.2f, 1f);
        [SerializeField] private Color dawnColor = new Color(1f, 0.6f, 0.3f, 1f);
        [SerializeField] private Color dayColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color duskColor = new Color(1f, 0.4f, 0.1f, 1f);

        [Header("Shadow Settings")]
        [SerializeField] private bool useShadows = true;
        [SerializeField] private float minShadowIntensity = 0f;
        [SerializeField] private float maxShadowIntensity = 1f;

        [Header("Reflection Probe")]
        [SerializeField] private bool useReflectionProbeIntensity = true;
        [SerializeField] private float dayProbeIntensity = 1f;
        [SerializeField] private float nightProbeIntensity = 0f;

        [Header("Fog")]
        [Tooltip("Контроллер применяет эти настройки тумана по игровому времени суток.")]
        [SerializeField] private bool useFog = true;
        [Tooltip("Linear: дистанции Start/End. Exponential и Exponential Squared: Density.")]
        [SerializeField] private FogMode fogMode = FogMode.Exponential;
        [ColorUsage(false)]
        [SerializeField] private Color nightFogColor = new Color(0.05f, 0.05f, 0.12f);
        [ColorUsage(false)]
        [SerializeField] private Color dayFogColor = new Color(0.6f, 0.75f, 1f);

        [Header("Fog - Linear Distances")]
        [Tooltip("Только Linear. Дистанция начала дымки от камеры днём, в единицах мира.")]
        [SerializeField, Min(0f)] private float dayFogStartDistance = 140f;
        [Tooltip("Только Linear. Дистанция полного перехода в цвет тумана днём.")]
        [SerializeField, Min(0.01f)] private float dayFogEndDistance = 650f;
        [Tooltip("Только Linear. Дистанция начала дымки от камеры ночью.")]
        [SerializeField, Min(0f)] private float nightFogStartDistance = 100f;
        [Tooltip("Только Linear. Дистанция полного перехода в цвет тумана ночью.")]
        [SerializeField, Min(0.01f)] private float nightFogEndDistance = 450f;

        [Header("Fog - Exponential Density")]
        [Tooltip("Только Exponential / Exponential Squared. В Linear не используется.")]
        [SerializeField, Min(0f)] private float nightFogDensity = 0.015f;
        [Tooltip("Только Exponential / Exponential Squared. В Linear не используется.")]
        [SerializeField, Min(0f)] private float dayFogDensity = 0.003f;

        [Header("Fog - World Mask")]
        [Tooltip("Дымка с маской в мировых XZ. Выключите для сравнения со встроенным туманом Unity.")]
        [SerializeField] private bool useMaskedFog;
        [Tooltip("WorldFogRenderFeature на активном URP Renderer. При отсутствии используется встроенный Fog.")]
        [SerializeField] private WorldFogRenderFeature maskedFogFeature;
        [Tooltip("Центр плато в мировых координатах X/Z.")]
        [SerializeField] private Vector2 fogMaskCenterXZ = Vector2.zero;
        [Tooltip("Радиус области с силой Plateau Fog Strength. Дальше начинается плавный переход.")]
        [SerializeField, Min(0f)] private float fogClearRadius = 100f;
        [Tooltip("Ширина перехода от плато к полной силе дымки внешнего леса.")]
        [SerializeField, Min(0.01f)] private float fogTransitionWidth = 100f;
        [Tooltip("Множитель дымки внутри радиуса плато в игровой камере. 1 сохраняет прежнюю силу.")]
        [SerializeField, Range(0f, 1f)] private float plateauFogStrength = 1f;
        [Tooltip("Множитель над плато в камере меню. Плавно смешивается по переходу камеры.")]
        [SerializeField, Range(0f, 1f)] private float menuPlateauFogStrength = 0.12f;
        [Tooltip("Множитель дымки за внешней границей перехода. 1 — обычная дистанционная дымка.")]
        [SerializeField, Range(0f, 2f)] private float forestFogStrength = 1f;
        [Tooltip("Максимальная непрозрачность дымки. 1 позволяет полностью скрывать дальние поверхности.")]
        [SerializeField, Range(0f, 1f)] private float maximumFogOpacity = 0.95f;

        [Header("Skybox")]
        [SerializeField] private bool useSkyboxExposure = true;
        [SerializeField] private AnimationCurve skyboxExposureCurve;
        [SerializeField] private Material daySkyboxMaterial;
        [SerializeField] private Material nightSkyboxMaterial;
        [Tooltip("Длительность плавного перехода между скайбоксами (в долях суток). 0.05 = ~1.2 часа")]
        [SerializeField] private float skyboxBlendRange = 0.05f;
        [Tooltip("Множитель яркости дневного скайбокса. 1 = как в исходном материале")]
        [SerializeField, Range(0f, 8f)] private float daySkyboxExposure = 1f;
        [Tooltip("Множитель яркости ночного скайбокса. 1 = как в исходном материале")]
        [SerializeField, Range(0f, 8f)] private float nightSkyboxExposure = 1f;

        // Материал-блендер (создаётся в Awake, если оба скайбокса — кубемапы)
        private Material _blendMaterial;
        private bool _blendMaterialReady;
        private float _dayBaseExposure = 1f;
        private float _nightBaseExposure = 1f;

        private Light _sunRotationLight;
        private float _sunYaw;
        private float _sunRoll;
        private WorldFogRenderFeature _lastMaskedFogFeature;

        private void Awake()
        {
            // Инициализируем кривые при старте (OnValidate не вызывается в runtime)
            if (lightIntensityCurve == null || lightIntensityCurve.keys.Length == 0)
            {
                lightIntensityCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.25f, 1f),
                    new Keyframe(0.75f, 1f),
                    new Keyframe(1f, 0f)
                );
            }

            if (skyboxExposureCurve == null || skyboxExposureCurve.keys.Length == 0)
            {
                skyboxExposureCurve = new AnimationCurve(
                    new Keyframe(0f, 0.1f),
                    new Keyframe(0.25f, 1f),
                    new Keyframe(0.75f, 1f),
                    new Keyframe(1f, 0.1f)
                );
            }

            SetupBlendMaterial();
        }

        /// <summary>
        /// Пытаемся собрать материал-блендер из двух кубемап-скайбоксов.
        /// Если не получилось (например, скайбоксы 6-Sided) — работаем через резкое переключение.
        /// </summary>
        private void SetupBlendMaterial()
        {
            if (daySkyboxMaterial == null || nightSkyboxMaterial == null) return;

            Texture dayTex = GetCubemap(daySkyboxMaterial);
            Texture nightTex = GetCubemap(nightSkyboxMaterial);
            if (dayTex == null || nightTex == null)
            {
                Debug.LogWarning("[TimeLightController] Скайбоксы не являются кубемапами (_Tex) — плавный блендинг недоступен, будет резкое переключение.");
                return;
            }

            Shader blendShader = Shader.Find("Custom/Skybox/CubemapBlend");
            if (blendShader == null)
            {
                Debug.LogWarning("[TimeLightController] Шейдер Custom/Skybox/CubemapBlend не найден — будет резкое переключение.");
                return;
            }

            _blendMaterial = new Material(blendShader)
            {
                name = "SkyboxBlend (Runtime)"
            };
            _blendMaterial.SetTexture("_Tex1", dayTex);
            _blendMaterial.SetTexture("_Tex2", nightTex);

            // Копируем _Tint и _Exposure из исходных материалов,
            // чтобы яркость совпадала со встроенным Skybox/Cubemap
            CopySkyboxProperty(daySkyboxMaterial, nightSkyboxMaterial, "_Tint",
                m => m.SetColor("_TintColor", m.GetColor("_Tint")));
            CopySkyboxProperty(daySkyboxMaterial, nightSkyboxMaterial, "_Exposure",
                m => _dayBaseExposure = m.GetFloat("_Exposure"));
            CopySkyboxProperty(nightSkyboxMaterial, daySkyboxMaterial, "_Exposure",
                m => _nightBaseExposure = m.GetFloat("_Exposure"));

            RenderSettings.skybox = _blendMaterial;
            _blendMaterialReady = true;
            Debug.Log("[TimeLightController] Плавный блендинг скайбоксов включён.");
        }

        /// <summary>Копирует свойство из основного материала (или из запасного, если в основном его нет).</summary>
        private void CopySkyboxProperty(Material primary, Material fallback, string prop, System.Action<Material> apply)
        {
            if (primary != null && primary.HasProperty(prop)) { apply(primary); return; }
            if (fallback != null && fallback.HasProperty(prop)) { apply(fallback); }
        }

        /// <summary>Достаём кубемапу из скайбокс-материала (Skybox/Cubemap использует _Tex).</summary>
        private static Texture GetCubemap(Material skyboxMat)
        {
            if (skyboxMat.HasProperty("_Tex"))
                return skyboxMat.GetTexture("_Tex");
            if (skyboxMat.HasProperty("_MainTex"))
                return skyboxMat.GetTexture("_MainTex");
            return null;
        }

        private void OnValidate()
        {
            dayFogStartDistance = Mathf.Max(0f, dayFogStartDistance);
            dayFogEndDistance = Mathf.Max(dayFogStartDistance + 0.01f, dayFogEndDistance);
            nightFogStartDistance = Mathf.Max(0f, nightFogStartDistance);
            nightFogEndDistance = Mathf.Max(nightFogStartDistance + 0.01f, nightFogEndDistance);
            dayFogDensity = Mathf.Max(0f, dayFogDensity);
            nightFogDensity = Mathf.Max(0f, nightFogDensity);
            fogClearRadius = Mathf.Max(0f, fogClearRadius);
            fogTransitionWidth = Mathf.Max(0.01f, fogTransitionWidth);

            // Настройка кривых в редакторе (дублирует Awake)
            if (lightIntensityCurve == null || lightIntensityCurve.keys.Length == 0)
            {
                lightIntensityCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.25f, 1f),
                    new Keyframe(0.75f, 1f),
                    new Keyframe(1f, 0f)
                );
            }

            if (skyboxExposureCurve == null || skyboxExposureCurve.keys.Length == 0)
            {
                skyboxExposureCurve = new AnimationCurve(
                    new Keyframe(0f, 0.1f),
                    new Keyframe(0.25f, 1f),
                    new Keyframe(0.75f, 1f),
                    new Keyframe(1f, 0.1f)
                );
            }
        }

        private void UpdateSunRotation(float sunAngle)
        {
            if (_sunRotationLight != dayLight)
            {
                // Euler-представление меняется около 90°. Не читаем Y/Z из результата прошлого кадра.
                Vector3 initialAngles = dayLight.transform.localEulerAngles;
                _sunYaw = initialAngles.y;
                _sunRoll = initialAngles.z;
                _sunRotationLight = dayLight;
            }

            dayLight.transform.localRotation = Quaternion.Euler(sunAngle, _sunYaw, _sunRoll);
        }

        private void LateUpdate()
        {
            if (dayLight == null)
                return;

            if (!SimWorld.TryGet(out var em, out var bag)
                || !SimWorld.TryGetGameTime(em, bag, out var gameTime)
                || gameTime.DayDuration <= 0f)
                return;

            // Доля дня: 0 = полночь, 0.25 = утро, 0.5 = полдень, 0.75 = вечер, 1 = полночь
            float dayProgress = Mathf.Repeat(gameTime.ElapsedInDay / gameTime.DayDuration, 1f);
            float dayness = GetDayness(dayProgress);

            // 1. Угол солнца (дуга по небу)
            float sunAngle = Mathf.Lerp(minSunAngle, maxSunAngle, dayProgress);
            UpdateSunRotation(sunAngle);

            // Кривая задаёт форму дневного света; ночью сохраняется заданный минимум.
            float lightBlend = dayness * Mathf.Clamp01(lightIntensityCurve.Evaluate(dayProgress));
            dayLight.intensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity, lightBlend);

            if (useAmbientLighting)
            {
                // Фоновый свет независим от яркости солнца и видимого скайбокса.
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.Lerp(nightAmbientColor, dayAmbientColor, dayness);
            }

            // 3. Цвет солнца
            Color sunColor = GetSunColor(dayProgress);
            dayLight.color = sunColor;

            // 4. Тени: ночью нет, днём полная
            float shadowIntensity = useShadows
                ? Mathf.SmoothStep(minShadowIntensity, maxShadowIntensity, lightBlend)
                : 0f;
            dayLight.shadowStrength = shadowIntensity;

            // 5. Reflection Probe — меняем intensity по времени суток
            if (useReflectionProbeIntensity)
            {
                float probeIntensity = Mathf.Lerp(nightProbeIntensity, dayProbeIntensity, dayness);
                var probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>();
                foreach (var probe in probes)
                {
                    probe.intensity = probeIntensity;
                }
            }

            // 6. Fog — отдельные параметры дня и ночи, общий переход с освещением.
            ApplyFog(dayness);

            // 7. Skybox — плавная смена материала
            ApplySkyboxMaterial(dayProgress);
        }

        private void ApplyFog(float dayness)
        {
            var menuCamera = PauseCameraSwitch.Current;
            ApplyFogForView(dayness, menuCamera != null ? menuCamera.MenuBlendProgress : 0f);
        }

        private void ApplyFogForView(float dayness, float menuBlend)
        {
            if (_lastMaskedFogFeature != maskedFogFeature)
            {
                if (_lastMaskedFogFeature != null) _lastMaskedFogFeature.ClearFog();
                _lastMaskedFogFeature = maskedFogFeature;
            }
            bool masked = useMaskedFog && maskedFogFeature != null && maskedFogFeature.IsReady;
            // The two paths are exclusive: the screen pass must receive unfogged scene colour.
            RenderSettings.fog = useFog && !masked;
            if (!useFog)
            {
                if (maskedFogFeature != null) maskedFogFeature.ClearFog();
                return;
            }

            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = Color.Lerp(nightFogColor, dayFogColor, dayness);

            float start = Mathf.Max(0f, Mathf.Lerp(nightFogStartDistance, dayFogStartDistance, dayness));
            float end = Mathf.Max(start + 0.01f, Mathf.Lerp(nightFogEndDistance, dayFogEndDistance, dayness));
            float density = Mathf.Max(0f, Mathf.Lerp(nightFogDensity, dayFogDensity, dayness));
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
            RenderSettings.fogDensity = density;

            if (masked)
            {
                maskedFogFeature.SetFog(new WorldFogSettings
                {
                    Enabled = true,
                    Color = RenderSettings.fogColor,
                    Distance = new Vector4(start, end, density, (float)fogMode),
                    Mask = new Vector4(fogMaskCenterXZ.x, fogMaskCenterXZ.y,
                        Mathf.Max(0f, fogClearRadius), Mathf.Max(0.01f, fogTransitionWidth)),
                    Strength = new Vector4(Mathf.Lerp(plateauFogStrength, menuPlateauFogStrength, menuBlend),
                        forestFogStrength, maximumFogOpacity, 0f)
                });
            }
            else if (maskedFogFeature != null)
            {
                maskedFogFeature.ClearFog();
            }
        }

        private void OnDisable()
        {
            if (maskedFogFeature != null) maskedFogFeature.ClearFog();
            if (_lastMaskedFogFeature != null && _lastMaskedFogFeature != maskedFogFeature)
                _lastMaskedFogFeature.ClearFog();
            _lastMaskedFogFeature = null;
        }

        // ==================== SKYBOX ====================
        private float GetDayness(float dayProgress)
        {
            float range = Mathf.Clamp(skyboxBlendRange, 0.001f, 0.15f);
            if (dayProgress < 0.5f)
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f - range, 0.15f + range, dayProgress));

            return Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.85f - range, 0.85f + range, dayProgress));
        }

        void ApplySkyboxMaterial(float dayProgress)
        {
            if (daySkyboxMaterial == null && nightSkyboxMaterial == null) return;

            // --- Плавный блендинг (если материал-блендер создан) ---
            if (_blendMaterialReady)
            {
                // dayness: 0 = ночь, 1 = день
                // Рассвет: плавно 0.15±range, закат: плавно 0.85±range
                float dayness = GetDayness(dayProgress);

                _blendMaterial.SetFloat("_Blend", dayness);

                // Итоговая экспозура = базовая (из материала) * множитель из Inspector * кривая
                if (useSkyboxExposure)
                {
                    _blendMaterial.SetFloat("_Exposure1", _dayBaseExposure * daySkyboxExposure * skyboxExposureCurve.Evaluate(dayProgress));
                    _blendMaterial.SetFloat("_Exposure2", _nightBaseExposure * nightSkyboxExposure);
                }
                else
                {
                    _blendMaterial.SetFloat("_Exposure1", _dayBaseExposure * daySkyboxExposure);
                    _blendMaterial.SetFloat("_Exposure2", _nightBaseExposure * nightSkyboxExposure);
                }
                return;
            }

            // --- Fallback: резкое переключение (скайбоксы не кубемапы) ---
            // Применяем exposure к активному материалу
            if (useSkyboxExposure && RenderSettings.skybox != null)
            {
                float exposure = skyboxExposureCurve.Evaluate(dayProgress);
                RenderSettings.skybox.SetFloat("_Exposure", exposure);
            }

            // Порог для переключения: ночь (0.85-1.0 и 0-0.15) → night, день → day
            bool isNight = dayProgress >= 0.85f || dayProgress < 0.15f;
            Material target = isNight ? nightSkyboxMaterial : daySkyboxMaterial;

            if (RenderSettings.skybox != target && target != null)
            {
                RenderSettings.skybox = target;
            }
        }

        /// <summary>
        /// Цвет солнца по фазе дня.
        /// 0.0-0.15 ночь -> рассвет, 0.15-0.25 рассвет -> утро,
        /// 0.25-0.75 день, 0.75-0.85 вечер, 0.85-1.0 ночь.
        /// </summary>
        private Color GetSunColor(float t)
        {
            if (t < 0.15f)
                return Color.Lerp(nightColor, dawnColor, t / 0.15f);
            if (t < 0.25f)
                return Color.Lerp(dawnColor, dayColor, (t - 0.15f) / 0.1f);
            if (t < 0.75f)
                return dayColor;
            if (t < 0.85f)
                return Color.Lerp(dayColor, duskColor, (t - 0.75f) / 0.1f);
            return Color.Lerp(duskColor, nightColor, (t - 0.85f) / 0.15f);
        }

        // Отладочная отрисовка дуги солнца в сцене
        private void OnDrawGizmosSelected()
        {
            if (dayLight == null) return;

            Gizmos.color = new Color(1f, 0.9f, 0.5f, 0.5f);

            int segments = 64;
            Vector3 lastPos = dayLight.transform.position;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.Lerp(minSunAngle, maxSunAngle, t);
                // Рисуем дугу относительно позиции света
                Vector3 dir = Quaternion.Euler(angle, 0, 0) * Vector3.up;
                Vector3 pos = dayLight.transform.position + dir * 5f;

                if (i > 0)
                    Gizmos.DrawLine(lastPos, pos);

                lastPos = pos;
            }
        }
    }
}
