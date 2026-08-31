using UnityEngine;
using Unity.Entities;
using TheyWillDescend.Simulation.Time;
using TheyWillDescend.Simulation.Session;

namespace TheyWillDescend.Presentation.Environment
{
    /// <summary>
    /// Связывает GameTime (ECS) с Directional Light и Skybox в Unity.
    /// Pull-паттерн: в LateUpdate читает ECS и применяет к Unity-объектам.
    /// Не пишет в ECS.
    /// </summary>
    public class TimeLightController : MonoBehaviour
    {
        private bool _ecsReady = false;
        private float _initTimer = 0f;
        
        private void Update()
        {
            // Ждём инициализации ECS (до 5 секунд)
            if (!_ecsReady)
            {
                _initTimer += Time.deltaTime;
                if (_initTimer > 5f)
                {
                    Debug.LogError("[TimeLightController] ECS не инициализировался за 5 секунд!");
                    _ecsReady = true; // Остановить спамер
                    return;
                }
                if (SimWorld.TryGet(out var em, out var bag))
                {
                    var simControl = em.GetComponentData<SimControl>(bag);
                    if (simControl.IsRunning)
                    {
                        _ecsReady = true;
                        Debug.Log("[TimeLightController] ECS готов!");
                    }
                }
            }
        }

        [Header("Light Setup")]
        [SerializeField] private Light dayLight;

        [Header("Day/Night Curve (Intensity)")]
        [SerializeField] private AnimationCurve lightIntensityCurve;

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
        [SerializeField] private bool useFog = true;
        [SerializeField] private float nightFogDensity = 0.015f;
        [SerializeField] private float dayFogDensity = 0.003f;
        [SerializeField] private Color nightFogColor = new Color(0.05f, 0.05f, 0.12f);
        [SerializeField] private Color dayFogColor = new Color(0.6f, 0.75f, 1f);

        [Header("Skybox")]
        [SerializeField] private bool useSkyboxExposure = true;
        [SerializeField] private AnimationCurve skyboxExposureCurve;
        [SerializeField] private Material daySkyboxMaterial;
        [SerializeField] private Material nightSkyboxMaterial;
        [Tooltip("Длительность плавного перехода между скайбоксами (в долях суток). 0.05 = ~1.2 часа")]
        [SerializeField] private float skyboxBlendRange = 0.05f;

        // Материал-блендер (создаётся в Awake, если оба скайбокса — кубемапы)
        private Material _blendMaterial;
        private bool _blendMaterialReady;

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
            RenderSettings.skybox = _blendMaterial;
            _blendMaterialReady = true;
            Debug.Log("[TimeLightController] Плавный блендинг скайбоксов включён.");
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

        private void LateUpdate()
        {
            if (!_ecsReady) return;
            
            if (dayLight == null)
            {
                Debug.LogWarning("[TimeLightController] dayLight не назначен!");
                return;
            }

            // Читаем из ECS через SimWorld (канон проекта — как в TimeWidget)
            if (!SimWorld.TryGet(out var em, out var bag))
            {
                Debug.LogWarning("[TimeLightController] SimWorld.TryGet failed");
                return;
            }

            var simControl = em.GetComponentData<SimControl>(bag);

            // Если часы на паузе - не обновляем (свет стоит на последнем кадре)
            if (!simControl.IsRunning) return;

            var gameTime = TryGetGameTime(em, out var gt) ? gt : default;
            if (gameTime.DayDuration <= 0f)
            {
                Debug.LogWarning("[TimeLightController] DayDuration = 0");
                return;
            }

            // Доля дня: 0 = полночь, 0.25 = утро, 0.5 = полдень, 0.75 = вечер, 1 = полночь
            float dayProgress = gameTime.ElapsedInDay / gameTime.DayDuration;

            // 1. Угол солнца (дуга по небу)
            float sunAngle = Mathf.Lerp(minSunAngle, maxSunAngle, dayProgress);
            dayLight.transform.localEulerAngles = new Vector3(sunAngle, dayLight.transform.localEulerAngles.y, dayLight.transform.localEulerAngles.z);

            // 2. Интенсивность по кривой
            float intensity = lightIntensityCurve.Evaluate(dayProgress);
            dayLight.intensity = intensity;

            // 3. Цвет солнца
            Color sunColor = GetSunColor(dayProgress);
            dayLight.color = sunColor;

            // 4. Тени: ночью нет, днём полная
            float shadowIntensity = Mathf.SmoothStep(minShadowIntensity, maxShadowIntensity, intensity);
            dayLight.shadowStrength = shadowIntensity;

            // 5. Reflection Probe — меняем intensity по времени суток
            if (useReflectionProbeIntensity)
            {
                float probeIntensity = Mathf.Lerp(nightProbeIntensity, dayProbeIntensity, intensity);
                var probes = UnityEngine.Object.FindObjectsOfType<ReflectionProbe>();
                foreach (var probe in probes)
                {
                    probe.intensity = probeIntensity;
                }
            }

            // 6. Fog — плотнее ночью, прозрачнее днём
            if (useFog)
            {
                RenderSettings.fog = true;
                float fogDensity = Mathf.Lerp(nightFogDensity, dayFogDensity, intensity);
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogColor = Color.Lerp(nightFogColor, dayFogColor, intensity);
            }

            // 7. Skybox — плавная смена материала
            ApplySkyboxMaterial(dayProgress);
        }

        // ==================== SKYBOX ====================
        void ApplySkyboxMaterial(float dayProgress)
        {
            if (daySkyboxMaterial == null && nightSkyboxMaterial == null) return;

            // --- Плавный блендинг (если материал-блендер создан) ---
            if (_blendMaterialReady)
            {
                // dayness: 0 = ночь, 1 = день
                // Рассвет: плавно 0.15±range, закат: плавно 0.85±range
                float range = Mathf.Max(0.001f, skyboxBlendRange);
                float dayness;
                if (dayProgress < 0.5f)
                    dayness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f - range, 0.15f + range, dayProgress));
                else
                    dayness = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.85f - range, 0.85f + range, dayProgress));

                _blendMaterial.SetFloat("_Blend", dayness);

                // Exposure по кривой применяется к дневной кубемапе,
                // ночная всегда с натуральной яркостью (1.0)
                if (useSkyboxExposure)
                {
                    _blendMaterial.SetFloat("_Exposure1", skyboxExposureCurve.Evaluate(dayProgress));
                    _blendMaterial.SetFloat("_Exposure2", 1f);
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

        /// <summary>
        /// Читаем GameTime через EntityQuery (канон — как в TimeWidget).
        /// </summary>
        static bool TryGetGameTime(EntityManager em, out GameTime time)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameTime>());
            if (query.IsEmptyIgnoreFilter)
            {
                time = default;
                return false;
            }

            time = query.GetSingleton<GameTime>();
            return true;
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
