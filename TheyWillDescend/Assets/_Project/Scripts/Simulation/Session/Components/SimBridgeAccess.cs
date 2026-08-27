using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    public static class SimBridgeAccess
    {
        public static bool TryGet(EntityManager em, out Entity session)
        {
            session = default;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<SimControl>());
            if (query.IsEmptyIgnoreFilter)
                return false;
            session = query.GetSingletonEntity();
            return true;
        }
    }
}
