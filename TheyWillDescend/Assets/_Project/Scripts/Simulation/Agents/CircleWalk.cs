using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Temporary movement recipe. The system writes LocalTransform — that is the pose.
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

        public readonly void GetPose(out float3 position, out float3 facing)
        {
            position = new float3(
                Center.x + math.cos(AngleRadians) * Radius,
                Center.y,
                Center.z + math.sin(AngleRadians) * Radius);
            facing = new float3(
                -math.sin(AngleRadians) * Direction,
                0f,
                math.cos(AngleRadians) * Direction);
        }

        public readonly LocalTransform ToLocalTransform()
        {
            GetPose(out var position, out var facing);
            return LocalTransform.FromPositionRotation(
                position,
                quaternion.LookRotationSafe(facing, math.up()));
        }
    }
}
