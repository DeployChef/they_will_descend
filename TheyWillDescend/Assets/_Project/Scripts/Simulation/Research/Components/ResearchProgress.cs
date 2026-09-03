using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// Per-run progress on a <see cref="TechCard"/>. Switching the active tech
    /// does not clear AccumulatedHours.
    /// </summary>
    public struct ResearchProgress : IComponentData
    {
        public float AccumulatedHours;
        public byte Completed;
        public byte CostPaid;

        public bool IsCompleted => Completed != 0;
        public bool IsCostPaid => CostPaid != 0;
    }
}
