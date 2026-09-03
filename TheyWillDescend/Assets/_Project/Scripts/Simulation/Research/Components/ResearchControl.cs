using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// Run-level research knobs. Active progress lives on the tech card.
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
