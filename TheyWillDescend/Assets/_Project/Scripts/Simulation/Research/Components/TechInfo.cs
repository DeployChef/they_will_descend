using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// Immutable catalog row for one technology. Runtime progress is
    /// <see cref="ResearchLine"/>. Heap-backed: Summary is too large for a chunk.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TechInfo : IBufferElementData
    {
        public FixedString64Bytes TechId;
        public FixedString64Bytes DisplayName;
        public FixedString512Bytes Summary;
        public float RequiredHours;
        public int RequiredTier;
        public int TreeColumn;
        public int TreeRow;
        public TechEffectKind EffectKind;
        public FixedString64Bytes EffectTarget;
        public int EffectTier;
    }
}
