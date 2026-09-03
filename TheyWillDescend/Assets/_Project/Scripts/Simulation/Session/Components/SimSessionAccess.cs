using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Gods;
using TheyWillDescend.Simulation.Research;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Resolves the singleton entity that owns session-root state and buffers.
    /// </summary>
    public static class SimSessionAccess
    {
        public static bool TryGet(EntityManager em, out Entity session)
        {
            session = default;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<SimSession>());
            if (query.IsEmptyIgnoreFilter)
                return false;
            session = query.GetSingletonEntity();
            return true;
        }

        public static bool TryGetResearch(EntityManager em, Entity session, out Entity research)
        {
            research = default;
            if (!em.HasComponent<ResearchLink>(session))
                return false;
            research = em.GetComponentData<ResearchLink>(session).Entity;
            return research != Entity.Null && em.Exists(research);
        }

        public static bool HasLifecycleQueues(EntityManager em, Entity session)
        {
            if (!em.HasComponent<SimControl>(session)
                || !em.HasComponent<AgentIdSequence>(session)
                || !em.HasComponent<PendingScenarioSpawns>(session)
                || !em.HasComponent<CityGrid>(session)
                || !em.HasComponent<SimPrototypes>(session)
                || !em.HasBuffer<SimClockCommand>(session)
                || !em.HasBuffer<DespawnAllAgentsCommand>(session)
                || !em.HasBuffer<DespawnAllBuildingsCommand>(session)
                || !em.HasBuffer<SpawnAgentCommand>(session)
                || !em.HasBuffer<PlaceBuildingCommand>(session)
                || !em.HasBuffer<PendingScenarioPlace>(session)
                || !em.HasBuffer<OccupiedCell>(session)
                || !em.HasBuffer<BaseBuildingPrototype>(session)
                || !em.HasBuffer<BaseBuildingCatalogCost>(session)
                || !em.HasBuffer<BaseBuildingCatalogRecipe>(session)
                || !em.HasBuffer<BuildingPrototype>(session)
                || !em.HasBuffer<BuildingCatalogCost>(session)
                || !em.HasBuffer<BuildingCatalogRecipe>(session)
                || !em.HasBuffer<BuildingRejectedEvent>(session)
                || !em.HasBuffer<AssignWorkerCommand>(session)
                || !em.HasBuffer<UnassignWorkerCommand>(session)
                || !em.HasBuffer<SetWorkplacePausedCommand>(session)
                || !em.HasBuffer<SetPyramidFeedCommand>(session)
                || !TryGetResearch(em, session, out var research)
                || !em.HasBuffer<SetActiveResearchCommand>(research)
                || !em.HasBuffer<ResearchLine>(research)
                || !em.HasBuffer<TechInfo>(research)
                || !em.HasBuffer<UnlockedBuilding>(research)
                || !em.HasComponent<ResearchControl>(research)
                || !em.HasComponent<ResearchCapacity>(research))
                return false;
            return true;
        }

        public static bool AreLifecycleQueuesDrained(EntityManager em, Entity session)
        {
            if (!HasLifecycleQueues(em, session))
                return false;

            return em.GetComponentData<PendingScenarioSpawns>(session).Workers <= 0
                && em.GetBuffer<SimClockCommand>(session).Length == 0
                && em.GetBuffer<DespawnAllAgentsCommand>(session).Length == 0
                && em.GetBuffer<DespawnAllBuildingsCommand>(session).Length == 0
                && em.GetBuffer<SpawnAgentCommand>(session).Length == 0
                && em.GetBuffer<PlaceBuildingCommand>(session).Length == 0
                && em.GetBuffer<PendingScenarioPlace>(session).Length == 0
                && em.GetBuffer<AssignWorkerCommand>(session).Length == 0
                && em.GetBuffer<UnassignWorkerCommand>(session).Length == 0
                && em.GetBuffer<SetWorkplacePausedCommand>(session).Length == 0
                && em.GetBuffer<SetPyramidFeedCommand>(session).Length == 0
                && ResearchCommandsDrained(em, session);
        }

        static bool ResearchCommandsDrained(EntityManager em, Entity session)
        {
            return TryGetResearch(em, session, out var research)
                && em.GetBuffer<SetActiveResearchCommand>(research).Length == 0;
        }
    }
}
