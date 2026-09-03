using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(AdvanceAgentCommuteSystem))]
    public partial struct AdvanceConstructionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<Construction>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var control = SystemAPI.GetSingleton<SimControl>();
            if (!control.IsRunning)
                return;
            var dt = control.DeltaTime;
            if (dt <= 0f)
                return;

            var arrivedBySite = new NativeHashMap<int, int>(8, state.WorldUpdateAllocator);
            foreach (var assignment in SystemAPI.Query<RefRO<AgentAssignment>>())
            {
                var job = assignment.ValueRO;
                if (job.ConstructionBuildingId == 0 || job.Arrived == 0)
                    continue;
                arrivedBySite.TryGetValue(job.ConstructionBuildingId, out var arrived);
                arrivedBySite[job.ConstructionBuildingId] = arrived + 1;
            }

            var finished = new NativeList<Entity>(8, state.WorldUpdateAllocator);
            foreach (var (construction, building, entity) in
                     SystemAPI.Query<RefRW<Construction>, RefRO<Building>>().WithEntityAccess())
            {
                if (!arrivedBySite.TryGetValue(building.ValueRO.Id, out var arrived) || arrived < 1)
                    continue;

                var site = construction.ValueRO;
                if (site.IsDismantling)
                {
                    site.Elapsed -= dt;
                    if (site.Elapsed < 0f)
                        site.Elapsed = 0f;
                }
                else
                    site.Elapsed += dt;

                construction.ValueRW = site;
                if (site.IsComplete)
                    finished.Add(entity);
            }

            var em = state.EntityManager;
            for (var i = 0; i < finished.Length; i++)
                FinishSite(em, finished[i]);
        }

        static void FinishSite(EntityManager em, Entity site)
        {
            if (!em.Exists(site) || !em.HasComponent<Construction>(site))
                return;

            if (em.GetComponentData<Construction>(site).IsDismantling)
            {
                BuildingDismantle.Complete(em, site);
                return;
            }

            var buildingId = em.HasComponent<Building>(site)
                ? em.GetComponentData<Building>(site).Id
                : 0;
            em.RemoveComponent<Construction>(site);
            if (buildingId > 0)
                BuildingDismantle.ReleaseCrew(em, buildingId);
#if UNITY_EDITOR
            if (em.Exists(site) && em.HasComponent<Building>(site))
            {
                var building = em.GetComponentData<Building>(site);
                em.SetName(site, $"Building_{building.Id}");
            }
#endif
        }
    }
}
