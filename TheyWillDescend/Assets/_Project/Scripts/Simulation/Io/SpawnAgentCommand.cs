using TheyWillDescend.Simulation.Agents;
using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// Player/load intent. Kind is sim identity, not a mesh.
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
        public AgentKind Kind;
    }
}
