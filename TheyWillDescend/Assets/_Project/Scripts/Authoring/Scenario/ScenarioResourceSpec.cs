using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Authoring.Scenario
{
    /// <summary>
    /// Bake-only starting ledger. Runtime amounts live on session <c>ResourceAmount</c>.
    /// </summary>
    public struct ScenarioResourceSpec : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float Amount;
    }
}
