using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Agents
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
        public int AgentId;
        public int WorkplaceBuildingId;
        public byte Arrived;
        public byte Moving;
        public byte HasPose;
        public byte PlazaWalking;
        public float PlazaTimer;
        public float PlazaAngle;
        public float PlazaRadius;
        public AgentKind Kind;
    }
}
