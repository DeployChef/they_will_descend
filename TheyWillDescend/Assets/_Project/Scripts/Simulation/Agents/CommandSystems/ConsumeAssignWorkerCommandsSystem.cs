using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumePlaceBuildingCommandsSystem))]
    public partial struct ConsumeAssignWorkerCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimBridge>();
        }

        public void OnUpdate(ref SystemState state) => Run(state.EntityManager);

        public static void Run(EntityManager em)
        {
            if (!SimBridgeAccess.TryGet(em, out var session))
                return;

            var commands = em.GetBuffer<AssignWorkerCommand>(session);
            if (commands.Length == 0)
                return;

            var copy = commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            for (var i = 0; i < copy.Length; i++)
                Assign(em, copy[i].BuildingId, copy[i].AgentId);
            copy.Dispose();
        }

        static void Assign(EntityManager em, int buildingId, int preferredAgentId)
        {
            if (buildingId <= 0)
                return;
            if (!TryGetBuilding(em, buildingId, out var buildingEntity))
                return;
            if (em.HasComponent<Construction>(buildingEntity) || em.HasComponent<Headquarters>(buildingEntity))
                return;

            var workplace = em.HasComponent<Workplace>(buildingEntity)
                ? em.GetComponentData<Workplace>(buildingEntity)
                : default;
            if (workplace.WorkerAgentId != 0)
                return;

            Entity agentEntity;
            int agentId;
            if (preferredAgentId > 0)
            {
                if (!TryGetAgent(em, preferredAgentId, out agentEntity, out agentId))
                    return;
            }
            else if (!TryGetIdleAgent(em, out agentEntity, out agentId))
            {
                return;
            }

            if (!em.HasComponent<Workplace>(buildingEntity))
                em.AddComponent<Workplace>(buildingEntity);
            em.SetComponentData(buildingEntity, new Workplace
            {
                WorkerAgentId = agentId,
                Working = 0
            });

            if (!em.HasComponent<AgentAssignment>(agentEntity))
                em.AddComponent<AgentAssignment>(agentEntity);
            em.SetComponentData(agentEntity, new AgentAssignment
            {
                WorkplaceBuildingId = buildingId,
                Arrived = 0
            });
        }

        static bool TryGetBuilding(EntityManager em, int buildingId, out Entity entity)
        {
            entity = Entity.Null;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            for (var i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].Id != buildingId)
                    continue;
                entity = entities[i];
                buildings.Dispose();
                return true;
            }

            buildings.Dispose();
            return false;
        }

        static bool TryGetIdleAgent(EntityManager em, out Entity entity, out int agentId)
        {
            entity = Entity.Null;
            agentId = 0;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<AgentId>(),
                ComponentType.ReadOnly<AgentAssignment>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            var ids = query.ToComponentDataArray<AgentId>(Allocator.Temp);
            var assignments = query.ToComponentDataArray<AgentAssignment>(Allocator.Temp);
            for (var i = 0; i < ids.Length; i++)
            {
                if (assignments[i].WorkplaceBuildingId != 0)
                    continue;
                entity = entities[i];
                agentId = ids[i].Value;
                ids.Dispose();
                assignments.Dispose();
                return true;
            }

            ids.Dispose();
            assignments.Dispose();
            return false;
        }

        static bool TryGetAgent(EntityManager em, int agentId, out Entity entity, out int id)
        {
            entity = Entity.Null;
            id = 0;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<AgentId>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            var ids = query.ToComponentDataArray<AgentId>(Allocator.Temp);
            for (var i = 0; i < ids.Length; i++)
            {
                if (ids[i].Value != agentId)
                    continue;
                entity = entities[i];
                id = agentId;
                ids.Dispose();
                return true;
            }

            ids.Dispose();
            return false;
        }
    }
}
