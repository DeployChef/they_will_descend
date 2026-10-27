using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Stories;
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

            var arrivedBySite = new NativeHashMap<Entity, int>(8, state.WorldUpdateAllocator);
            foreach (var assignment in SystemAPI.Query<RefRO<AgentAssignment>>())
            {
                var job = assignment.ValueRO;
                if (job.ConstructionSite == Entity.Null || job.Arrived == 0)
                    continue;
                arrivedBySite.TryGetValue(job.ConstructionSite, out var arrived);
                arrivedBySite[job.ConstructionSite] = arrived + 1;
            }

            var finished = new NativeList<Entity>(8, state.WorldUpdateAllocator);
            foreach (var (construction, entity) in
                     SystemAPI.Query<RefRW<Construction>>().WithAll<Building>().WithEntityAccess())
            {
                if (!arrivedBySite.TryGetValue(entity, out var arrived) || arrived < 1)
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

            var typeId = em.HasComponent<Building>(site)
                ? em.GetComponentData<Building>(site).TypeId
                : default;
            em.RemoveComponent<Construction>(site);
            NoteStoryCompletion(em, site, typeId);
#if UNITY_EDITOR
            if (em.Exists(site) && em.HasComponent<Building>(site))
            {
                var building = em.GetComponentData<Building>(site);
                em.SetName(site, $"Building_{building.Id}");
            }
#endif
        }

        static void NoteStoryCompletion(EntityManager em, Entity site, Unity.Collections.FixedString64Bytes typeId)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<SimSession>());
            if (query.IsEmptyIgnoreFilter)
                return;
            var session = query.GetSingletonEntity();
            if (!em.HasBuffer<StoryFact>(session))
                return;
            em.GetBuffer<StoryFact>(session).Add(new StoryFact
            {
                Kind = StoryFactKind.BuildingCompleted,
                Id = typeId,
                Subject = site
            });
        }
    }
}
