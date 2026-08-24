using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.City
{
    public struct CityGrid : IComponentData
    {
        public RadialGridConfig Config;
        public float3 Center;
        public int NextBuildingId;
        public byte Ready;
        /// <summary>Fallback duration when the building definition has none.</summary>
        public float ConstructionDuration;
    }

    public struct OccupiedCell : IBufferElementData
    {
        public int Cluster;
        public int Radial;
    }
}
