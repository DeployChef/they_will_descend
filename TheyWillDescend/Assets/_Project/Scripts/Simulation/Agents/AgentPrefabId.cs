using Unity.Collections;
using Unity.Entities;

namespace _Project.Scripts.Simulation.Agents
{
    /// <summary>
    /// Stable content id for save/load. Not Entity index.version.
    /// </summary>
    public struct AgentPrefabId : IComponentData
    {
        public FixedString64Bytes Value;
    }
}
