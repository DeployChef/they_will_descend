using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Baked onto the session. Play consumes it once into SpawnAgentCommand.
    /// Must not Instantiate during bake — Live Conversion duplicates EntityGuid.
    /// </summary>
    public struct PendingScenarioSpawns : IComponentData
    {
        public int Workers;
    }
}
