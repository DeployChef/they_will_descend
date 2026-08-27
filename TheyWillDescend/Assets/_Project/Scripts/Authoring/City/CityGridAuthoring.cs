using TheyWillDescend.Simulation.City;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    /// <summary>
    /// Polar grid + occupancy on the session entity. Must sit on the same GO as
    /// <see cref="SimControlAuthoring"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CityGridAuthoring : MonoBehaviour
    {
        [SerializeField] RadialGridConfig cityGrid = RadialGridConfig.Default;
        [SerializeField] float constructionDuration = 8f;

        public RadialGridConfig Config => cityGrid.IsValid ? cityGrid : RadialGridConfig.Default;

        class Baker : Baker<CityGridAuthoring>
        {
            public override void Bake(CityGridAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var config = authoring.Config;
                AddComponent(entity, new CityGrid
                {
                    Config = config,
                    Center = float3.zero,
                    Ready = 1,
                    NextBuildingId = 1,
                    ConstructionDuration = authoring.constructionDuration > 0.001f
                        ? authoring.constructionDuration
                        : 8f
                });
                AddBuffer<OccupiedCell>(entity);
                AddBuffer<PlaceBuildingCommand>(entity);
                AddBuffer<BuildingRejectedEvent>(entity);
                AddBuffer<PendingScenarioPlace>(entity);
            }
        }
    }
}
