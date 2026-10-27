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
            em.CompleteAllTrackedJobs();
            if (Contains(em.GetBuffer<RoadSegment>(session), a, b))
                return;

            if (!em.HasComponent<BuildingIdSequence>(session))
                return;
            var sequence = em.GetComponentData<BuildingIdSequence>(session);
            var siteId = sequence.Take();
            em.SetComponentData(session, sequence);
            var site = SpawnSite(em, grid, typeId, duration, crewSlots, siteId, a, b, instant);
            var segmentId = network.NextSegmentId++;
            var roads = em.GetBuffer<RoadSegment>(session);
            if (Contains(roads, a, b))
                return;
            roads.Add(new RoadSegment
            {
                Id = segmentId,
                Site = site,
                RingA = a.Ring,
                FineA = a.Fine,
                RingB = b.Ring,
                FineB = b.Fine,
                Protected = protectedFlag
            });
        }

        public static void SeedInnerRing(
            EntityManager em,
            Entity session,
            ref CityGrid grid,
            ref RoadNetwork network)
        {
            em.CompleteAllTrackedJobs();
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
            var site = Entity.Null;
            for (var i = 0; i < roads.Length; i++)
            {
                if (roads[i].Id != segmentId)
                    continue;
                if (roads[i].Protected != 0)
                    return false;
                site = roads[i].Site;
                break;
            }

            if (site == Entity.Null)
                return false;

            BuildingDismantle.Begin(em, site);
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

        static Entity SpawnSite(
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
            RunWorld.Own(em, entity);
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
            return entity;
        }
    }
}
