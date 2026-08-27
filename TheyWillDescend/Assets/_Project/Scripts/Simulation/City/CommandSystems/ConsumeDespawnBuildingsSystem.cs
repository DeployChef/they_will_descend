using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumeDespawnAgentsSystem))]
    public partial struct ConsumeDespawnBuildingsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimBridge>();
            state.RequireForUpdate<CityGrid>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Run(state.EntityManager);
        }

        public static void Run(EntityManager em)
        {
            if (!SimBridgeAccess.TryGet(em, out var session))
                return;

            var bridge = em.GetComponentData<SimBridge>(session);
            if (bridge.DespawnAllBuildings == 0)
                return;

            using var buildings = em.CreateEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.Exclude<Headquarters>());
            SimEntityDestroy.DestroyQuery(em, buildings);
            em.GetBuffer<OccupiedCell>(session).Clear();
            var grid = em.GetComponentData<CityGrid>(session);
            grid.NextBuildingId = 1;
            em.SetComponentData(session, grid);
            bridge.DespawnAllBuildings = 0;
            em.SetComponentData(session, bridge);
        }
    }
}
