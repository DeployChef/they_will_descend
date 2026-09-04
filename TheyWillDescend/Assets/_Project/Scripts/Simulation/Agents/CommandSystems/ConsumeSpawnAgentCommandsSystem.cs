using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Agents
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumePendingScenarioSpawnsSystem))]
    [UpdateBefore(typeof(ConsumePlaceBuildingCommandsSystem))]
    public partial struct ConsumeSpawnAgentCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
            state.RequireForUpdate<AgentIdSequence>();
            state.RequireForUpdate<SimPrototypes>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Run(state.EntityManager);
        }

        public static void Run(EntityManager em)
        {
            if (!SimSessionAccess.TryGet(em, out var session))
                return;

            var commands = em.GetBuffer<SpawnAgentCommand>(session);
            if (commands.Length == 0)
                return;

            var catalog = em.GetComponentData<SimPrototypes>(session);
            if (catalog.Agent == Entity.Null)
                return;

            var sequence = em.GetComponentData<AgentIdSequence>(session);
            var copy = commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            for (var i = 0; i < copy.Length; i++)
                Spawn(em, ref sequence, catalog.Agent, copy[i]);
            copy.Dispose();
            em.SetComponentData(session, sequence);
        }

        static void Spawn(
            EntityManager em,
            ref AgentIdSequence sequence,
            Entity prototype,
            in SpawnAgentCommand command)
        {
            sequence.NextAgentId += 1;
            var agentId = command.AgentId > 0 ? command.AgentId : sequence.NextAgentId;
            if (sequence.NextAgentId < agentId)
                sequence.NextAgentId = agentId;

            var facing = math.lengthsq(command.Facing) > 0.001f
                ? command.Facing
                : new float3(0f, 0f, 1f);
            var position = command.HasPose != 0 ? command.Position : float3.zero;
            var transform = LocalTransform.FromPositionRotation(
                position,
                quaternion.LookRotationSafe(facing, math.up()));

            var entity = em.Instantiate(prototype);
            em.SetComponentData(entity, new AgentId { Value = agentId });
            em.SetComponentData(entity, new AgentType { Kind = command.Kind });
            var protoSpeed = em.HasComponent<AgentLocomotion>(prototype)
                ? em.GetComponentData<AgentLocomotion>(prototype).Speed
                : 0f;
            if (protoSpeed <= 0.001f)
                protoSpeed = 2f;
            em.SetComponentData(entity, new AgentLocomotion
            {
                Speed = command.Speed > 0.001f ? command.Speed : protoSpeed,
                Target = command.Target,
                Moving = command.Moving
            });
            if (!em.HasComponent<AgentAssignment>(entity))
                em.AddComponent<AgentAssignment>(entity);
            em.SetComponentData(entity, new AgentAssignment
            {
                WorkplaceBuildingId = command.WorkplaceBuildingId,
                Arrived = command.Arrived
            });
            if (!em.HasComponent<AgentPlazaIdle>(entity))
                em.AddComponent<AgentPlazaIdle>(entity);
            em.SetComponentData(entity, new AgentPlazaIdle
            {
                Timer = command.PlazaTimer > 0f ? command.PlazaTimer : 2.5f,
                Angle = command.PlazaAngle,
                Radius = command.PlazaRadius,
                Walking = command.PlazaWalking
            });
            SimEntityPose.Apply(em, entity, transform);
#if UNITY_EDITOR
            em.SetName(entity, $"Agent_{agentId}");
#endif
        }
    }
}
