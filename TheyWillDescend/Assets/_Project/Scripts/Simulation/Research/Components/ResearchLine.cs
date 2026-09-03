using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// Per-tech progress for this run. Switching Active does not clear AccumulatedHours.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ResearchLine : IBufferElementData
    {
        public FixedString64Bytes TechId;
        public float AccumulatedHours;
        public byte Completed;
        public byte CostPaid;

        public bool IsCompleted => Completed != 0;
        public bool IsCostPaid => CostPaid != 0;
    }
}
