using UnityEngine;

namespace TheyWillDescend.Presentation.Environment
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class PointGrassWindController : MonoBehaviour
    {
        private static readonly int WindDirectionId = Shader.PropertyToID("_PG_VectorA");
        private static readonly int WindOffsetId = Shader.PropertyToID("_PG_VectorB");
        private static readonly int WindScaleId = Shader.PropertyToID("_PG_ValueA");

        [SerializeField, Tooltip("Spatial scale of the wind noise.")]
        private float windScale = 0.13f;
        [SerializeField, Tooltip("Minimum and maximum wind noise strength.")]
        private Vector2 noiseRange = new(0.3f, 0.9f);
        [SerializeField, Tooltip("Direction and strength of the wind.")]
        private Vector3 windDirection = new(0.65f, 0f, 0.35f);
        [SerializeField, Tooltip("World-space speed of the wind waves.")]
        private Vector3 windScroll = new(0.15f, 0f, 0.08f);

        private Vector3 _noiseOffset;

#if UNITY_EDITOR
        private double _previousEditorTime;
#endif

        private void OnEnable()
        {
            _noiseOffset = Vector3.zero;
            ApplyWind();

#if UNITY_EDITOR
            _previousEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update += UpdateInEditor;
#endif
        }

#if UNITY_EDITOR
        private void OnDisable()
        {
            UnityEditor.EditorApplication.update -= UpdateInEditor;
        }

        private void UpdateInEditor()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            float deltaTime = (float)(now - _previousEditorTime);
            _previousEditorTime = now;

            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                AdvanceWind(deltaTime);
        }
#endif

        private void LateUpdate()
        {
            if (Application.IsPlaying(gameObject))
                AdvanceWind(Time.deltaTime);
        }

        private void AdvanceWind(float deltaTime)
        {
            _noiseOffset += windScroll * deltaTime;
            ApplyWind();
        }

        // Point Grass reads the strength range from the fourth components.
        private void ApplyWind()
        {
            Shader.SetGlobalVector(WindDirectionId,
                new Vector4(windDirection.x, windDirection.y, windDirection.z, noiseRange.x));
            Shader.SetGlobalVector(WindOffsetId,
                new Vector4(_noiseOffset.x, _noiseOffset.y, _noiseOffset.z, noiseRange.y));
            Shader.SetGlobalFloat(WindScaleId, windScale);
        }
    }
}
