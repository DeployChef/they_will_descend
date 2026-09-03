using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// Parent tech this card needs completed. Lives on the card, not a session table.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct TechPrerequisite : IBufferElementData
    {
        public FixedString64Bytes RequiresTechId;
    }
}
