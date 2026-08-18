using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.Simulation.Agents
{
    /// <summary>
    /// Where the agent entity is. This is the character's location in simulation.
    /// Movement systems write it; presentation copies it to the mesh Transform.
    /// Facing is "look direction", not an animation clip.
    /// </summary>
    public struct AgentPosition : IComponentData
    {
        public float3 Value;
        public float3 Facing;
    }
}
