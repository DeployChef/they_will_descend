using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    public struct SetActiveResearchCommand : IBufferElementData
    {
        public FixedString64Bytes TechId;
    }
}
