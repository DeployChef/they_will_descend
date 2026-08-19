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
        public float3 Position;
        public float3 Facing;
        public float3 Target;
        public float Speed;
        public byte Moving;
        public byte HasPose;
        public AgentKind Kind;
    }
}
