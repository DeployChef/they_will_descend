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
    }

    public struct OccupiedCell : IBufferElementData
    {
        public int Cluster;
        public int Radial;
    }

    public struct Building : IComponentData
    {
        public int Id;
        public int WidthClusters;
        public int DepthRadialRings;
        public int AnchorCluster;
        public int AnchorRadial;
    }

    public struct PlaceBuildingCommand : IBufferElementData
    {
        public int WidthClusters;
        public int DepthRadialRings;
        public int AnchorCluster;
        public int AnchorRadial;
    }
}
