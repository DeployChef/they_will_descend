using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Gods
{
    public struct SetPyramidFeedRequest : IComponentData
    {
        public FixedString64Bytes ResourceId;
        public float PerHour;
    }
}