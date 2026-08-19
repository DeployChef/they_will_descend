using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// Player/load intent: create an agent. VisualId is a catalog key, not a GameObject.
    /// </summary>
    public struct SpawnAgentCommand : IBufferElementData
    {
        public float3 Center;
        public float Radius;
        public float Speed;
        public float Direction;
        public float AngleRadians;
        public float3 Position;
        public float3 Facing;
        public byte HasPose;
        public FixedString64Bytes VisualId;
    }
}
