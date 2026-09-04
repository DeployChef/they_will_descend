using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Session-owned sequence for generated agent identifiers.
    /// Explicit restored identifiers advance the same sequence.
    /// </summary>
    public struct AgentIdSequence : IComponentData
    {
        public int NextAgentId;
    }
}
