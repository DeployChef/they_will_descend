using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Agents
{
    public struct SpawnAgentRequest : IComponentData
    {
        public AgentKind Kind;
        public float Speed;
        public float3 Position;
        public float3 Facing;
        public float3 Target;
        public byte Moving;
        public int WorkplaceBuildingId;
        public byte Arrived;
        public int AgentId;
        public byte HasPose;
        public float PlazaTimer;
        public float PlazaAngle;
        public float PlazaRadius;
        public byte PlazaWalking;
    }
}