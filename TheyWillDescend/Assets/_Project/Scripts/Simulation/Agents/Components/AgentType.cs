using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Sim identity. Presentation maps this to a Mixamo; ECS does not store a prefab slot.
    /// </summary>
    public enum AgentKind : byte
    {
        Worker = 0
    }

    public struct AgentType : IComponentData
    {
        public AgentKind Kind;
    }
}
