using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.Simulation.Agents
{
    /// <summary>
    /// Walks on a horizontal circle. Simulation-owned; presentation reads via <see cref="AgentPresentation"/>.
    /// </summary>
    public struct CircleWalk : IComponentData
    {
        public float3 Center;
        public float Radius;
        /// <summary>Revolutions per second.</summary>
        public float Speed;
        /// <summary>+1 or -1.</summary>
        public float Direction;
        public float AngleRadians;
    }
}
