using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Owns the run's entities in the default world. Do not wipe with UniversalQuery —
    /// package singletons live there.
    /// </summary>
    public static class RunWorld
    {
        public static void Wipe(EntityManager em)
        {
            using var query = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<RunScoped>() },
                Options = EntityQueryOptions.IncludePrefab
                    | EntityQueryOptions.IncludeDisabledEntities
            });
            if (!query.IsEmptyIgnoreFilter)
                em.DestroyEntity(query);
        }

        public static void Own(EntityManager em, Entity entity)
        {
            if (entity == Entity.Null || !em.Exists(entity))
                return;
            if (!em.HasComponent<RunScoped>(entity))
                em.AddComponent<RunScoped>(entity);
        }
    }
}
