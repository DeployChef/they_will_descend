using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    public struct PlaceBuildingRequest : IComponentData
    {
        public FixedString64Bytes TypeId;
        public int AnchorCluster;
        public int AnchorRadial;
    }
}
