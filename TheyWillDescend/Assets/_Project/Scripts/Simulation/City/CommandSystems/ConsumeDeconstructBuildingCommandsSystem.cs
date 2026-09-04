using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    public partial struct ConsumeDeconstructBuildingCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SimSessionAccess.TryGet(em, out var session))
                return;

            var query = SystemAPI.QueryBuilder().WithAll<DemolishBuildingRequest>().Build();
            if (query.IsEmptyIgnoreFilter)
                return;

            var lifecycle = em.GetComponentData<SimSession>(session);
            using var requestEntities = query.ToEntityArray(Allocator.Temp);
            using var requests = query.ToComponentDataArray<DemolishBuildingRequest>(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                if (lifecycle.IsReady)
                {
                    Apply(em, requests[i].BuildingId);
                }
                em.DestroyEntity(requestEntities[i]);
            }
        }



        static void Apply(EntityManager em, int buildingId)
        {
            if (buildingId <= 0 || !TryGetBuilding(em, buildingId, out var entity))
                return;

            if (em.HasComponent<Workplace>(entity))
            {
                var wp = em.GetComponentData<Workplace>(entity);
                wp.DesiredWorkers = 0;
                wp.Paused = 1;
                wp.AssignedCount = 0;
                wp.WorkingCount = 0;
                em.SetComponentData(entity, wp);
            }

            using var agentsQuery = em.CreateEntityQuery(ComponentType.ReadWrite<AgentAssignment>());
            using var agentEntities = agentsQuery.ToEntityArray(Allocator.Temp);
            var assignments = agentsQuery.ToComponentDataArray<AgentAssignment>(Allocator.Temp);
            for (var i = 0; i < assignments.Length; i++)
            {
                if (assignments[i].WorkplaceBuildingId != buildingId)
                    continue;
                var job = assignments[i];
                job.WorkplaceBuildingId = 0;
                if (!job.HasConstructionTask)
                    job.Arrived = 0;
                em.SetComponentData(agentEntities[i], job);
            }
            assignments.Dispose();

            if (em.HasComponent<Construction>(entity))
            {
                var site = em.GetComponentData<Construction>(entity);
                if (site.IsDismantling)
                    return;
                site.Dismantling = 1;
                em.SetComponentData(entity, site);
                if (site.IsComplete)
                    BuildingDismantle.Complete(em, entity);
                return;
            }

            var duration = em.HasComponent<BuildingType>(entity)
                ? em.GetComponentData<BuildingType>(entity).ConstructionDuration
                : 0f;
            if (duration < 0f)
                duration = 0f;
            var construction = new Construction
            {
                Elapsed = duration,
                Duration = duration,
                Dismantling = 1
            };
            if (construction.IsComplete)
            {
                BuildingDismantle.Complete(em, entity);
                return;
            }

            em.AddComponentData(entity, construction);
#if UNITY_EDITOR
            em.SetName(entity, $"BuildingSite_{buildingId}");
#endif
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
    }
}
