using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumeDespawnAgentsSystem))]
    [UpdateBefore(typeof(ConsumePendingScenarioSpawnsSystem))]
    public partial struct ConsumeDespawnBuildingsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
            state.RequireForUpdate<DespawnAllBuildingsCommand>();
            state.RequireForUpdate<CityGrid>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Run(state.EntityManager);
        }

        public static void Run(EntityManager em)
        {
            if (!SimSessionAccess.TryGet(em, out var session)
                || !em.HasBuffer<DespawnAllBuildingsCommand>(session))
                return;

            var commands = em.GetBuffer<DespawnAllBuildingsCommand>(session);
            if (commands.Length == 0)
                return;

            commands.Clear();
            using var buildings = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());

            SimEntityDestroy.DestroyQuery(em, buildings);
            em.GetBuffer<OccupiedCell>(session).Clear();
            if (em.HasBuffer<RoadSegment>(session))
                em.GetBuffer<RoadSegment>(session).Clear();
            if (em.HasComponent<RoadNetwork>(session))
            {
                var roads = em.GetComponentData<RoadNetwork>(session);
                roads.NextSegmentId = 1;
                em.SetComponentData(session, roads);
            }
            var grid = em.GetComponentData<CityGrid>(session);
            grid.NextBuildingId = 1;
            em.SetComponentData(session, grid);
        }
    }
}
