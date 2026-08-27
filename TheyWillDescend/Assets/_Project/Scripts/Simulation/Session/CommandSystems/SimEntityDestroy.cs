using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Session
{
    public static class SimEntityDestroy
    {
        public static void DestroyQuery(EntityManager em, EntityQuery query)
        {
            if (query.IsEmptyIgnoreFilter)
                return;

            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.Exists(entity))
                    continue;
                if (em.HasBuffer<LinkedEntityGroup>(entity))
                {
                    var group = em.GetBuffer<LinkedEntityGroup>(entity)
                        .ToNativeArray(Allocator.Temp);
                    em.DestroyEntity(group.Reinterpret<Entity>());
                    group.Dispose();
                }
                else
                    em.DestroyEntity(entity);
            }
        }
    }
}
