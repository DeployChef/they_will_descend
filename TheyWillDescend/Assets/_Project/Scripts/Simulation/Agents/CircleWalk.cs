using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.Simulation.Agents
{
    /// <summary>
    /// Temporary movement recipe: walk a horizontal circle.
    /// <see cref="AdvanceCircleWalkSystem"/> uses this to write <see cref="AgentPosition"/>.
    /// Center is the circle origin, not the character.
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

        public readonly AgentPosition ToPosition()
        {
            return new AgentPosition
            {
                Value = new float3(
                    Center.x + math.cos(AngleRadians) * Radius,
                    Center.y,
                    Center.z + math.sin(AngleRadians) * Radius),
                Facing = new float3(
                    -math.sin(AngleRadians) * Direction,
                    0f,
                    math.cos(AngleRadians) * Direction)
            };
        }
    }
}
