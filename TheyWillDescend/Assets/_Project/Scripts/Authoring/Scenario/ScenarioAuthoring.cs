using TheyWillDescend.Authoring.City;
using TheyWillDescend.Content;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Authoring.Scenario
{
    /// <summary>
    /// SubScene hook for a <see cref="ScenarioDefinition"/>. Must not sit on the
    /// SimControl GameObject — BakingOnlyEntity would strip the session singleton.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenarioAuthoring : MonoBehaviour
    {
        public const string PreviewRootName = "_ScenarioPreview";

        [SerializeField] ScenarioDefinition definition;

        public ScenarioDefinition Definition => definition;

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

        public bool TryGetPlacement(
            out RadialGridConfig config,
            out float3 center,
            out BuildingCatalogAuthoring catalog)
        {
            config = default;
            center = float3.zero;
            catalog = FindFirstObjectByType<BuildingCatalogAuthoring>();
            var grid = FindFirstObjectByType<CityGridAuthoring>();
            if (catalog == null || grid == null)
                return false;
            config = grid.Config;
            var hq = FindFirstObjectByType<HeadquarterAuthoring>();
            center = hq != null ? (float3)hq.transform.position : float3.zero;
            return config.IsValid;
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

        class ScenarioBaker : Baker<ScenarioAuthoring>
        {
            public override void Bake(ScenarioAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var buildings = AddBuffer<ScenarioBuildingSpec>(entity);
                var stock = AddBuffer<ScenarioResourceSpec>(entity);
                var so = authoring.definition;
                if (so == null)
                    return;
                DependsOn(so);
                var workers = so.StartingWorkers;
                if (workers > 0)
                    AddComponent(entity, new ScenarioPopulation { StartingWorkers = workers });
                var buildingRecords = so.Buildings;
                for (var i = 0; i < buildingRecords.Count; i++)
                {
                    var record = buildingRecords[i];
                    var typeId = ContentId.EncodeOrEmpty(record.TypeId);
                    if (typeId.IsEmpty)
                    {
                        Debug.LogError($"Scenario '{so.name}' building {i} has an empty typeId.");
                        continue;
                    }

                    buildings.Add(new ScenarioBuildingSpec
                    {
                        TypeId = typeId,
                        Cluster = record.Cluster,
                        Radial = record.Radial
                    });
                }

                var resourceRecords = so.StartingStock;
                if (resourceRecords != null)
                {
                    for (var i = 0; i < resourceRecords.Count; i++)
                    {
                        var record = resourceRecords[i];
                        if (record.Resource == null)
                            continue;
                        DependsOn(record.Resource);
                        stock.Add(new ScenarioResourceSpec
                        {
                            ResourceId = ContentId.EncodeOrEmpty(record.Resource.ResourceId),
                            Amount = record.Amount < 0f ? 0f : record.Amount
                        });
                    }
                }
            }
        }
    }
}
