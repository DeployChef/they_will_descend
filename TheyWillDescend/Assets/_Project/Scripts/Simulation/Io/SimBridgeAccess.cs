using TheyWillDescend.Simulation.City;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    public static class SimBridgeAccess
    {
        public static Entity GetOrCreate(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<SimBridge>());
            if (!query.IsEmptyIgnoreFilter)
            {
                var existing = query.GetSingletonEntity();
                EnsureBuffers(em, existing);
                return existing;
            }

            var entity = em.CreateEntity();
            em.AddComponent<SimBridge>(entity);
            EnsureBuffers(em, entity);
#if UNITY_EDITOR
            em.SetName(entity, "SimBridge");
#endif
            return entity;
        }

        public static Entity GetOrCreateCityGrid(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<CityGrid>());
            if (!query.IsEmptyIgnoreFilter)
            {
                var existing = query.GetSingletonEntity();
                if (!em.HasBuffer<OccupiedCell>(existing))
                    em.AddBuffer<OccupiedCell>(existing);
                return existing;
            }

            var entity = em.CreateEntity();
            em.AddComponent<CityGrid>(entity);
            em.AddBuffer<OccupiedCell>(entity);
#if UNITY_EDITOR
            em.SetName(entity, "CityGrid");
#endif
            return entity;
        }

        static void EnsureBuffers(EntityManager em, Entity entity)
        {
            if (!em.HasBuffer<SpawnAgentCommand>(entity))
                em.AddBuffer<SpawnAgentCommand>(entity);
            if (!em.HasBuffer<PlaceBuildingCommand>(entity))
                em.AddBuffer<PlaceBuildingCommand>(entity);
            if (!em.HasBuffer<AgentSpawnedEvent>(entity))
                em.AddBuffer<AgentSpawnedEvent>(entity);
            if (!em.HasBuffer<AgentDespawnedEvent>(entity))
                em.AddBuffer<AgentDespawnedEvent>(entity);
            if (!em.HasBuffer<DayChangedEvent>(entity))
                em.AddBuffer<DayChangedEvent>(entity);
            if (!em.HasBuffer<BuildingPlacedEvent>(entity))
                em.AddBuffer<BuildingPlacedEvent>(entity);
            if (!em.HasBuffer<BuildingDespawnedEvent>(entity))
                em.AddBuffer<BuildingDespawnedEvent>(entity);
            if (!em.HasBuffer<BuildingRejectedEvent>(entity))
                em.AddBuffer<BuildingRejectedEvent>(entity);
        }
    }
}
