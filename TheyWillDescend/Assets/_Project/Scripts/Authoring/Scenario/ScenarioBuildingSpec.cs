using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Authoring.Scenario
{
    /// <summary>
    /// Bake-only copy of <see cref="ScenarioDefinition"/> entries.
    /// Stripped with the authoring entity; runtime never sees this buffer.
    /// </summary>
    public struct ScenarioBuildingSpec : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public int Cluster;
        public int Radial;
    }
}
