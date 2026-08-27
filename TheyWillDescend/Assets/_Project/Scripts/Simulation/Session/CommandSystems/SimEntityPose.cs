using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Writes pose for Instantiate in the same frame Entities Graphics samples LocalToWorld.
    /// </summary>
    public static class SimEntityPose
    {
        public static void Apply(EntityManager em, Entity entity, LocalTransform transform)
        {
            if (!em.HasComponent<LocalTransform>(entity))
                em.AddComponent<LocalTransform>(entity);
            em.SetComponentData(entity, transform);
            WriteLocalToWorld(em, entity, transform.ToMatrix());

            if (!em.HasBuffer<LinkedEntityGroup>(entity))
                return;

            var group = em.GetBuffer<LinkedEntityGroup>(entity).ToNativeArray(Allocator.Temp);
            for (var i = 0; i < group.Length; i++)
            {
                var child = group[i].Value;
                if (child == entity || !em.Exists(child) || !em.HasComponent<LocalTransform>(child))
                    continue;

                var local = em.GetComponentData<LocalTransform>(child);
                var parentMatrix = transform.ToMatrix();
                if (em.HasComponent<Parent>(child))
                {
                    var parent = em.GetComponentData<Parent>(child).Value;
                    if (em.Exists(parent) && em.HasComponent<LocalToWorld>(parent))
                        parentMatrix = em.GetComponentData<LocalToWorld>(parent).Value;
                }

                WriteLocalToWorld(em, child, math.mul(parentMatrix, local.ToMatrix()));
            }

            group.Dispose();
        }

        static void WriteLocalToWorld(EntityManager em, Entity entity, float4x4 matrix)
        {
            var localToWorld = new LocalToWorld { Value = matrix };
            if (em.HasComponent<LocalToWorld>(entity))
                em.SetComponentData(entity, localToWorld);
            else
                em.AddComponentData(entity, localToWorld);
        }
    }
}
