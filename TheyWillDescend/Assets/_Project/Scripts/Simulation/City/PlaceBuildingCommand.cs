using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    public struct PlaceBuildingCommand : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public int WidthClusters;
        public int DepthRadialRings;
        public int AnchorCluster;
        public int AnchorRadial;
        public float ConstructionElapsed;
        public float ConstructionDuration;
        public byte InstantComplete;
        public int BuildingId;
    }
}
