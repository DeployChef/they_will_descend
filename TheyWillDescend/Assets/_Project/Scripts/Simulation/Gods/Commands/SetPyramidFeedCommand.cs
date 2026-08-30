using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Gods
{
    public struct SetPyramidFeedCommand : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float PerHour;
    }
}
