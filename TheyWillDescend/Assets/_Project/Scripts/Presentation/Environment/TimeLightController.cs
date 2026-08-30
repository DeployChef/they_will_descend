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
        [Header("Light Setup")]
        [SerializeField] private Light dayLight;

        [Header("Skybox")]
        [SerializeField] private Material skyboxMaterial;
        [SerializeField] private bool useSkyboxTint = true;

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

        private void OnValidate()
        {
            // Создаём кривую по умолчанию, если пуста
            if (lightIntensityCurve == null || lightIntensityCurve.keys.Length == 0)
            {
                lightIntensityCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.25f, 1f),
                    new Keyframe(0.75f, 1f),
                    new Keyframe(1f, 0f)
                );
            }
        }

        private void LateUpdate()
        {
            if (dayLight == null) return;

            // Читаем из ECS через SimWorld (канон проекта — как в TimeWidget)
            if (!SimWorld.TryGet(out var em, out var bag))
            {
                return;
            }

            var simControl = em.GetComponentData<SimControl>(bag);

            // Если часы на паузе - не обновляем (свет стоит на последнем кадре)
            if (!simControl.IsRunning) return;

            var gameTime = TryGetGameTime(em, out var gt) ? gt : default;
            if (gameTime.DayDuration <= 0f) return;

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

            // 5. Скайбокс tint
            if (useSkyboxTint && skyboxMaterial != null)
            {
                float brightness = Mathf.Lerp(0.15f, 1f, intensity);
                skyboxMaterial.SetColor("_Tint", new Color(brightness, brightness, brightness, 1f));
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
