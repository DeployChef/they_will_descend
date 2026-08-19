using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Motor only: go to a world point or stand.
    /// Who picked the point (house patrol, hunt, scout) is another component.
    /// Presentation pulls <see cref="Moving"/>.
    /// </summary>
    public struct AgentLocomotion : IComponentData
    {
        public float Speed;
        public float3 Target;
        public byte Moving;
    }
}
