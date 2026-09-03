using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    public struct SetActiveResearchRequest : IComponentData
    {
        public FixedString64Bytes TechId;
    }
}