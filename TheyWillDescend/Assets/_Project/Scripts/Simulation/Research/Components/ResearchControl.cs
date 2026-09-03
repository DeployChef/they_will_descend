using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// City research: one active tech, current tree tier. Progress lives on
    /// <see cref="ResearchLine"/> rows.
    /// </summary>
    public struct ResearchControl : IComponentData
    {
        public FixedString64Bytes ActiveTechId;
        public int UnlockedTier;

        public static ResearchControl Initial => new()
        {
            UnlockedTier = 1
        };
    }
}
