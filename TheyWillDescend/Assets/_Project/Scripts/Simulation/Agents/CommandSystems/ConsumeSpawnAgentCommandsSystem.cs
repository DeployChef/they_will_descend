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
            var em = state.EntityManager;
            if (!SimSessionAccess.TryGet(em, out var session))
                return;

            var query = SystemAPI.QueryBuilder().WithAll<SpawnAgentRequest>().Build();
            if (query.IsEmptyIgnoreFilter)
                return;

            var catalog = em.GetComponentData<SimPrototypes>(session);
            if (catalog.Agent == Entity.Null)
                return;

            var sequence = em.GetComponentData<AgentIdSequence>(session);
            using var requestEntities = query.ToEntityArray(Allocator.Temp);
            using var requests = query.ToComponentDataArray<SpawnAgentRequest>(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                var req = requests[i];
                Spawn(em, ref sequence, catalog.Agent, in req);
                em.DestroyEntity(requestEntities[i]);
            }


            em.SetComponentData(session, sequence);
        }

        static void Spawn(
            EntityManager em,
            ref AgentIdSequence sequence,
            Entity prototype,
            in SpawnAgentRequest command)

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
