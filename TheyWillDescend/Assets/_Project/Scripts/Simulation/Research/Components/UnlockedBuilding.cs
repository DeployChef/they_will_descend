using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// Building typeId unlocked by a completed tech. Place and the build
    /// catalog hide locked types until this row exists.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct UnlockedBuilding : IBufferElementData
    {
        public FixedString64Bytes TypeId;
    }
}
