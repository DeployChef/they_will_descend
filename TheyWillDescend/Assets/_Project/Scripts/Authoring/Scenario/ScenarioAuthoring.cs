using TheyWillDescend.Content;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Authoring.Scenario
{
    /// <summary>
    /// Visual editor for a <see cref="ScenarioDefinition"/>.
    /// Renders the radial grid in Scene View, previews starting houses,
    /// and synchronizes changes with the ScenarioDefinition ScriptableObject.
    /// Does not bake to SubScene; simulation reads ScenarioDefinition at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class ScenarioAuthoring : MonoBehaviour
    {
        public const string PreviewRootName = "_ScenarioPreview";

        [SerializeField] ScenarioDefinition definition;
        [SerializeField] BuildingCatalogAsset catalog;
        [SerializeField] RadialGridConfig gridConfig = RadialGridConfig.Default;
        [SerializeField] Transform centerTransform;

        public ScenarioDefinition Definition
        {
            get => definition;
            set => definition = value;
        }

        public BuildingCatalogAsset Catalog
        {
            get => catalog;
            set => catalog = value;
        }

        public RadialGridConfig GridConfig
        {
            get => gridConfig.IsValid ? gridConfig : RadialGridConfig.Default;
            set => gridConfig = value;
        }

        public Transform PreviewRoot
        {
            get
            {
                var root = transform.Find(PreviewRootName);
                if (root != null)
                    return root;
                var go = new GameObject(PreviewRootName);
                go.transform.SetParent(transform, false);
                return go.transform;
            }
        }

        void Awake()
        {
            if (Application.isPlaying)
            {
                var root = transform.Find(PreviewRootName);
                if (root != null)
                    root.gameObject.SetActive(false);
            }
        }

        public bool TryGetPlacement(
            out RadialGridConfig config,
            out float3 center,
            out BuildingCatalogAsset outCatalog)
        {
            config = GridConfig;
            center = float3.zero;
            if (centerTransform != null)
                center = (float3)centerTransform.position;
            else
            {
                var hq = GameObject.Find("Headquarters");
                if (hq != null)
                    center = (float3)hq.transform.position;
            }

            outCatalog = catalog;
#if UNITY_EDITOR
            if (outCatalog == null)
            {
                outCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>(
                    "Assets/_Project/Content/Buildings/DefaultBuildingCatalog.asset");
                if (catalog == null && outCatalog != null)
                    catalog = outCatalog;
            }
            if (definition == null)
            {
                definition = UnityEditor.AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(
                    "Assets/_Project/Content/Scenarios/DefaultScenario.asset");
            }
#endif
            return config.IsValid && outCatalog != null;
        }

        void OnDrawGizmosSelected()
        {
            if (!TryGetPlacement(out var config, out var center, out _))
                return;

            var maxRing = math.min(config.RingCount, 12);
            Gizmos.color = new Color(0.25f, 0.7f, 1f, 0.45f);
            for (var r = 0; r <= maxRing; r++)
                DrawRing(center, config.RingLineRadius(r));

            Gizmos.color = new Color(0.25f, 0.7f, 1f, 0.2f);
            var n = config.GetClusterCount(0);
            var inner = config.InnerRadius;
            var outer = config.RingLineRadius(maxRing);
            for (var i = 0; i < n; i += 6)
            {
                var turns = i / (float)n;
                Gizmos.DrawLine(
                    RadialGridMath.PolarToWorld(center, turns, inner),
                    RadialGridMath.PolarToWorld(center, turns, outer));
            }
        }

        static void DrawRing(float3 center, float radius)
        {
            const int segments = 96;
            var prev = RadialGridMath.PolarToWorld(center, 0f, radius);
            for (var i = 1; i <= segments; i++)
            {
                var next = RadialGridMath.PolarToWorld(center, i / (float)segments, radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}