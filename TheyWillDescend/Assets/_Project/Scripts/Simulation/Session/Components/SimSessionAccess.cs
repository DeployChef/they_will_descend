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
    }
}
