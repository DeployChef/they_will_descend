using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Shared type data on the house stamp, copied from <see cref="TheyWillDescend.Simulation.Content.BuildingDefinition"/>.
    /// Not instance id / anchor.
    /// </summary>
    public struct BuildingType : IComponentData
    {
        public FixedString64Bytes TypeId;
        public int WidthClusters;
        public int DepthRadialRings;
        public float ConstructionDuration;
        public int WorkplaceSlots;
        public FixedString64Bytes ProduceResourceId;
        public float ProducePerSecond;

        public BuildingFootprint Footprint => new()
        {
            WidthClusters = WidthClusters,
            DepthRadialRings = DepthRadialRings
        };
    }
}
