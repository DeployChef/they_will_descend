using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Presentation-facing command posting boundary. Gameplay posts commands and lets
    /// <see cref="CommandSystemGroup"/> consume them on the next simulation tick.
    /// </summary>
    public static class SimCommands
    {
        public static bool TryPost<T>(T command)
            where T : unmanaged, IBufferElementData
        {
            if (!SimWorld.TryGet(out var em, out var bag))
                return false;
            if (!em.HasBuffer<T>(bag))
            {
                using var query = em.CreateEntityQuery(ComponentType.ReadWrite<T>());
                if (query.CalculateEntityCount() != 1)
                    return false;
                bag = query.GetSingletonEntity();
            }
            em.GetBuffer<T>(bag).Add(command);
            return true;
        }

        public static bool Request<T>(in T request)
            where T : unmanaged, IComponentData
        {
            if (!SimWorld.TryGetEntityManager(out var em))
                return false;
            var entity = em.CreateEntity();
            em.AddComponentData(entity, request);
            return true;
        }
    }

}
