using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.City
{
    public struct CityGrid : IComponentData
    {
        public RadialGridConfig Config;
        public float3 Center;
        public byte Ready;
    }

    public struct OccupiedCell : IBufferElementData
    {
        public int Cluster;
        public int Radial;
        public Entity Building;
    }
}
