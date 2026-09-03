using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Type data copied onto a house instance from <see cref="BuildingPrototype"/>.
    /// Not instance id / anchor.
    /// </summary>
    public struct BuildingType : IComponentData
    {
        public FixedString64Bytes TypeId;
        public int WidthClusters;
        public int DepthRadialRings;
        public float ConstructionDuration;
        public int WorkplaceSlots;
        public int ConstructionCrewSlots;

        public BuildingFootprint Footprint => new()
        {
            WidthClusters = WidthClusters,
            DepthRadialRings = DepthRadialRings
        };
    }
}
