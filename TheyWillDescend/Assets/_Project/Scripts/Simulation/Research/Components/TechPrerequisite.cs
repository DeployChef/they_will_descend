using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    [InternalBufferCapacity(0)]
    public struct TechPrerequisite : IBufferElementData
    {
        public FixedString64Bytes TechId;
        public FixedString64Bytes RequiresTechId;
    }
}
