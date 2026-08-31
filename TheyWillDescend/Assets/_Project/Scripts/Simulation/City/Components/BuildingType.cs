using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Shared type data on the house stamp. Not instance id / anchor.
    /// </summary>
    public struct BuildingType : IComponentData
    {
        public FixedString64Bytes TypeId;
        public int WidthClusters;
        public int DepthRadialRings;
        public float ConstructionDuration;
        public int WorkplaceSlots;

        public BuildingFootprint Footprint => new()
        {
            WidthClusters = WidthClusters,
            DepthRadialRings = DepthRadialRings
        };
    }
}
