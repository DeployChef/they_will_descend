using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Where the agent entity is. Simulation write model. Views pull this; Transform is not stored here.
    /// </summary>
    public struct AgentPosition : IComponentData
    {
        public float3 Value;
        public float3 Facing;
    }
}
