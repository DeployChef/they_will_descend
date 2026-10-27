using Unity.Collections;
using Unity.Entities;
using TheyWillDescend.Simulation.City;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Presentation-facing posting boundary.
    /// Buffers live on the session. Requests are one-shot entities tagged <see cref="RunScoped"/>.
    /// <see cref="CommandSystemGroup"/> processes them on the next simulation tick.
    /// </summary>
    public static class SimCommands
    {
        public static bool TryPost<T>(T command)
            where T : unmanaged, IBufferElementData
        {
            if (!SimWorld.TryGet(out var em, out var bag))
                return false;
            if (!em.HasBuffer<T>(bag))
                return false;
            em.GetBuffer<T>(bag).Add(command);
            return true;
        }

        public static bool Request<T>(in T request)
            where T : unmanaged, IComponentData
        {
            if (!SimWorld.TryGetEntityManager(out var em))
                return false;
            var entity = em.CreateEntity();
            RunWorld.Own(em, entity);
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
            RunWorld.Own(em, entity);
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
