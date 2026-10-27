using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Agents
{
    public static class AgentSpawn
    {
        public static Entity Spawn(EntityManager em, Entity session, in SpawnAgentRequest command)
        {
            em.CompleteAllTrackedJobs();
            if (!em.HasComponent<SimPrototypes>(session)
                || !em.HasComponent<AgentIdSequence>(session))
                return Entity.Null;

            var catalog = em.GetComponentData<SimPrototypes>(session);
            if (catalog.Agent == Entity.Null || !em.Exists(catalog.Agent))
                return Entity.Null;

            var sequence = em.GetComponentData<AgentIdSequence>(session);
            var entity = Spawn(em, ref sequence, catalog.Agent, in command);
            em.SetComponentData(session, sequence);
            return entity;
        }

        public static Entity Spawn(
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
            RunWorld.Own(em, entity);
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
                Workplace = command.Workplace,
                Arrived = command.Arrived
            });
            if (!em.HasComponent<AgentPlazaIdle>(entity))
                em.AddComponent<AgentPlazaIdle>(entity);
            em.SetComponentData(entity, new AgentPlazaIdle
            {
                Timer = command.PlazaTimer > 0f ? command.PlazaTimer : 2.5f,
                Angle = command.PlazaAngle,
                Radius = command.PlazaRadius,
                Walking = command.PlazaWalking,
                Direction = command.PlazaDirection != 0
                    ? command.PlazaDirection
                    : PlazaRing.PickDirection(agentId)
            });
            SimEntityPose.Apply(em, entity, transform);
#if UNITY_EDITOR
            em.SetName(entity, $"Agent_{agentId}");
#endif
            return entity;
        }
    }
}
