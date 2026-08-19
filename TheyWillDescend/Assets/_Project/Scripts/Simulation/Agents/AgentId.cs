using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Instance id for the run. Bridge key for views/events. Not a mesh name, not Entity index.
    /// </summary>
    public struct AgentId : IComponentData
    {
        public int Value;
    }
}
