using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using TheyWillDescend.Simulation.City;

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

        public static bool RequestRoadStroke(NativeArray<RoadNode> nodes, int validCount)
        {
            if (!SimWorld.TryGetEntityManager(out var em))
                return false;
            if (validCount < 2 || nodes.Length < validCount)
                return false;
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new PlaceRoadStrokeRequest
            {
                ValidPointCount = validCount
            });
            var buffer = em.AddBuffer<RoadStrokePoint>(entity);
            for (var i = 0; i < validCount; i++)
                buffer.Add(new RoadStrokePoint { Ring = nodes[i].Ring, Fine = nodes[i].Fine });
            return true;
        }

        public static bool RequestDemolishRoad(int segmentId)
        {
            if (segmentId <= 0)
                return false;
            return Request(new DemolishRoadRequest { SegmentId = segmentId });
        }
    }

}
