using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// Immutable rule on a tech card. Progress is <see cref="ResearchProgress"/>.
    /// </summary>
    public struct TechInfo : IComponentData
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
