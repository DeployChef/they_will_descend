using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    public static class BuildingDismantle
    {
        public static void Begin(EntityManager em, Entity site)
        {
            if (site == Entity.Null || !em.Exists(site) || !em.HasComponent<Building>(site))
                return;

            var buildingId = em.GetComponentData<Building>(site).Id;

            if (em.HasComponent<Workplace>(site))
            {
                var wp = em.GetComponentData<Workplace>(site);
                wp.AssignedCount = 0;
                wp.WorkingCount = 0;
                em.SetComponentData(site, wp);
            }

            using var agentsQuery = em.CreateEntityQuery(ComponentType.ReadWrite<AgentAssignment>());
            using var agentEntities = agentsQuery.ToEntityArray(Allocator.Temp);
            var assignments = agentsQuery.ToComponentDataArray<AgentAssignment>(Allocator.Temp);
            for (var i = 0; i < assignments.Length; i++)
            {
                if (assignments[i].Workplace != site)
                    continue;
                var job = assignments[i];
                job.Workplace = Entity.Null;
                if (!job.HasConstructionTask)
                    job.Arrived = 0;
                em.SetComponentData(agentEntities[i], job);
            }

            assignments.Dispose();

            if (em.HasComponent<Construction>(site))
            {
                var construction = em.GetComponentData<Construction>(site);
                if (construction.IsDismantling)
                    return;
                construction.Dismantling = 1;
                em.SetComponentData(site, construction);
                if (construction.IsComplete)
                    Complete(em, site);
                return;
            }

            var duration = em.HasComponent<BuildingType>(site)
                ? em.GetComponentData<BuildingType>(site).ConstructionDuration
                : 0f;
            if (duration < 0f)
                duration = 0f;
            if (duration < 0.001f && em.HasComponent<RoadSpan>(site))
                duration = RoadMath.DefaultSectionSeconds;

            var next = new Construction
            {
                Elapsed = duration,
                Duration = duration,
                Dismantling = 1
            };
            if (next.IsComplete)
            {
                Complete(em, site);
                return;
            }

            em.AddComponentData(site, next);
#if UNITY_EDITOR
            em.SetName(site, em.HasComponent<RoadSpan>(site)
                ? $"RoadSite_{buildingId}"
                : $"BuildingSite_{buildingId}");
#endif
        }

        public static void Complete(EntityManager em, Entity site)
        {
            if (!em.Exists(site) || !em.HasComponent<Building>(site))
                return;

            var building = em.GetComponentData<Building>(site);
            if (SimSessionAccess.TryGet(em, out var session))
            {
                Refund(em, session, building.TypeId);
                FreeCells(em, session, site);
                RemoveRoad(em, session, site);
            }

            em.DestroyEntity(site);
        }

        static void RemoveRoad(EntityManager em, Entity session, Entity site)
        {
            if (site == Entity.Null || !em.HasBuffer<RoadSegment>(session))
                return;
            var roads = em.GetBuffer<RoadSegment>(session);
            for (var i = roads.Length - 1; i >= 0; i--)
            {
                if (roads[i].Site != site)
                    continue;
                roads.RemoveAt(i);
                break;
            }
        }

        static void Refund(EntityManager em, Entity session, in FixedString64Bytes typeId)
        {
            if (!em.HasBuffer<BuildingCatalogCost>(session) || !em.HasBuffer<ResourceAmount>(session))
                return;
            var info = em.HasBuffer<ResourceInfo>(session)
                ? em.GetBuffer<ResourceInfo>(session)
                : default;
            if (!info.IsCreated)
                return;
            BuildingCosts.Refund(
                em.GetBuffer<BuildingCatalogCost>(session),
                typeId,
                em.GetBuffer<ResourceAmount>(session),
                info);
        }

        static void FreeCells(EntityManager em, Entity session, Entity site)
        {
            if (site == Entity.Null || !em.HasBuffer<OccupiedCell>(session))
                return;

            var occupied = em.GetBuffer<OccupiedCell>(session);
            for (var j = occupied.Length - 1; j >= 0; j--)
            {
                if (occupied[j].Building != site)
                    continue;
                occupied.RemoveAt(j);
            }
        }
    }
}
