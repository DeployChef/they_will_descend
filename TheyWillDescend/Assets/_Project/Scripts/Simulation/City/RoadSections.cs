using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.City
{
    public static class RoadSections
    {
        public static void Spawn(
            EntityManager em,
            Entity session,
            ref CityGrid grid,
            ref RoadNetwork network,
            in FixedString64Bytes typeId,
            in RoadNode a,
            in RoadNode b,
            float duration,
            int crewSlots,
            bool instant,
            byte protectedFlag)
        {
            var roads = em.GetBuffer<RoadSegment>(session);
            if (Contains(roads, a, b))
                return;

            var siteId = grid.NextBuildingId + 1;
            grid.NextBuildingId = siteId;
            var segmentId = network.NextSegmentId++;
            roads.Add(new RoadSegment
            {
                Id = segmentId,
                SiteId = siteId,
                RingA = a.Ring,
                FineA = a.Fine,
                RingB = b.Ring,
                FineB = b.Fine,
                Protected = protectedFlag
            });
            SpawnSite(em, grid, typeId, duration, crewSlots, siteId, a, b, instant);
        }

        public static void SeedInnerRing(
            EntityManager em,
            Entity session,
            ref CityGrid grid,
            ref RoadNetwork network)
        {
            if (!grid.Config.IsValid || !em.HasBuffer<RoadSegment>(session))
                return;

            var typeId = new FixedString64Bytes(RoadNetwork.TypeId);
            var n = RoadMath.FineCount(grid.Config);
            for (var fine = 0; fine < n; fine += RoadMath.FinePerCluster)
            {
                var a = new RoadNode { Ring = 0, Fine = fine };
                var b = new RoadNode { Ring = 0, Fine = RoadMath.WrapFine(fine + RoadMath.FinePerCluster, n) };
                Spawn(em, session, ref grid, ref network, typeId, a, b, 0f, 1, true, 1);
            }
        }

        public static bool BeginDismantle(EntityManager em, Entity session, int segmentId)
        {
            if (segmentId <= 0 || !em.HasBuffer<RoadSegment>(session))
                return false;

            var roads = em.GetBuffer<RoadSegment>(session);
            var siteId = 0;
            for (var i = 0; i < roads.Length; i++)
            {
                if (roads[i].Id != segmentId)
                    continue;
                if (roads[i].Protected != 0)
                    return false;
                siteId = roads[i].SiteId;
                break;
            }

            if (siteId <= 0)
                return false;

            BuildingDismantle.Begin(em, siteId);
            return true;
        }

        public static bool Contains(DynamicBuffer<RoadSegment> roads, in RoadNode a, in RoadNode b)
        {
            for (var i = 0; i < roads.Length; i++)
            {
                if (RoadMath.SameSpan(roads[i], a, b))
                    return true;
            }

            return false;
        }

        static void SpawnSite(
            EntityManager em,
            in CityGrid grid,
            in FixedString64Bytes typeId,
            float duration,
            int crewSlots,
            int siteId,
            in RoadNode a,
            in RoadNode b,
            bool instant)
        {
            var aw = RoadMath.NodeWorld(grid.Center, grid.Config, a);
            var bw = RoadMath.NodeWorld(grid.Center, grid.Config, b);
            var mid = (aw + bw) * 0.5f;
            var delta = bw - aw;
            var yaw = math.atan2(delta.x, delta.y);
            var transform = LocalTransform.FromPositionRotationScale(
                new float3(mid.x, grid.Center.y, mid.y),
                quaternion.Euler(0f, yaw, 0f),
                1f);

            var entity = em.CreateEntity();
            em.AddComponentData(entity, new Building
            {
                Id = siteId,
                TypeId = typeId,
                WidthClusters = 0,
                DepthRadialRings = 0
            });
            em.AddComponentData(entity, new BuildingType
            {
                TypeId = typeId,
                ConstructionDuration = duration,
                ConstructionCrewSlots = crewSlots
            });
            em.AddComponentData(entity, new RoadSpan
            {
                RingA = a.Ring,
                FineA = a.Fine,
                RingB = b.Ring,
                FineB = b.Fine
            });
            if (!instant && duration > 0.001f)
            {
                em.AddComponentData(entity, new Construction
                {
                    Elapsed = 0f,
                    Duration = duration,
                    Dismantling = 0
                });
            }

            SimEntityPose.Apply(em, entity, transform);
#if UNITY_EDITOR
            em.SetName(entity, instant ? $"Road_{siteId}" : $"RoadSite_{siteId}");
#endif
        }
    }
}
