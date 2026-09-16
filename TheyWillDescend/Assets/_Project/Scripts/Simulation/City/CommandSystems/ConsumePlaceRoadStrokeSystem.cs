using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumePlaceBuildingCommandsSystem))]
    [UpdateBefore(typeof(FinalizeSimSessionLifecycleSystem))]
    public partial struct ConsumePlaceRoadStrokeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
            state.RequireForUpdate<CityGrid>();
            state.RequireForUpdate<RoadNetwork>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SimSessionAccess.TryGet(em, out var session))
                return;

            var grid = em.GetComponentData<CityGrid>(session);
            var network = em.GetComponentData<RoadNetwork>(session);
            if (network.SectionLength < 0.05f)
                network.SectionLength = RoadMath.SectionLength(grid.Config);

            if (em.GetComponentData<SimSession>(session).AcceptsSetupCommands)
                RoadSections.SeedInnerRing(em, session, ref grid, ref network);

            var query = SystemAPI.QueryBuilder()
                .WithAll<PlaceRoadStrokeRequest, RoadStrokePoint>()
                .Build();
            if (query.IsEmptyIgnoreFilter)
            {
                em.SetComponentData(session, grid);
                em.SetComponentData(session, network);
                return;
            }

            NativeArray<OccupiedCell> occupied = default;
            if (em.HasBuffer<OccupiedCell>(session))
            {
                var buffer = em.GetBuffer<OccupiedCell>(session);
                occupied = new NativeArray<OccupiedCell>(buffer.Length, Allocator.Temp);
                for (var i = 0; i < buffer.Length; i++)
                    occupied[i] = buffer[i];
            }
            else
            {
                occupied = new NativeArray<OccupiedCell>(0, Allocator.Temp);
            }

            var typeId = new FixedString64Bytes(RoadNetwork.TypeId);
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var e = 0; e < entities.Length; e++)
            {
                var entity = entities[e];
                var request = em.GetComponentData<PlaceRoadStrokeRequest>(entity);
                var points = em.GetBuffer<RoadStrokePoint>(entity);
                Commit(em, session, ref grid, ref network, occupied, typeId, request, points);
                em.DestroyEntity(entity);
            }

            occupied.Dispose();
            em.SetComponentData(session, grid);
            em.SetComponentData(session, network);
        }

        static void Commit(
            EntityManager em,
            Entity session,
            ref CityGrid grid,
            ref RoadNetwork network,
            NativeArray<OccupiedCell> occupied,
            in FixedString64Bytes typeId,
            in PlaceRoadStrokeRequest request,
            DynamicBuffer<RoadStrokePoint> points)
        {
            var count = request.ValidPointCount;
            if (count < 2 || points.Length < count)
            {
                Reject(em, session, BuildingRejectedEvent.InvalidCell);
                return;
            }

            var nodes = new NativeArray<RoadNode>(count, Allocator.Temp);
            for (var i = 0; i < count; i++)
                nodes[i] = new RoadNode { Ring = points[i].Ring, Fine = points[i].Fine };

            var valid = 1;
            for (var i = 1; i < count; i++)
            {
                if (RoadMath.EdgeBlocked(grid.Config, occupied, nodes[i - 1], nodes[i]))
                    break;
                valid = i + 1;
            }

            if (valid < 2)
            {
                nodes.Dispose();
                Reject(em, session, BuildingRejectedEvent.Overlap);
                return;
            }

            var corners = new NativeList<RoadNode>(8, Allocator.Temp);
            RoadMath.CollapseToSections(nodes, valid, corners, grid.Config);
            nodes.Dispose();
            if (corners.Length < 2)
            {
                corners.Dispose();
                Reject(em, session, BuildingRejectedEvent.InvalidCell);
                return;
            }

            var roads = em.GetBuffer<RoadSegment>(session);
            var newCount = 0;
            for (var i = 1; i < corners.Length; i++)
            {
                if (!RoadSections.Contains(roads, corners[i - 1], corners[i]))
                    newCount++;
            }

            if (newCount <= 0)
            {
                corners.Dispose();
                Reject(em, session, BuildingRejectedEvent.Overlap);
                return;
            }

            if (!TryPay(em, session, typeId, newCount))
            {
                corners.Dispose();
                Reject(em, session, BuildingRejectedEvent.Unaffordable);
                return;
            }

            var spec = default(BuildingPrototype);
            var hasSpec = em.HasBuffer<BuildingPrototype>(session)
                && BuildingCatalog.TryResolve(em.GetBuffer<BuildingPrototype>(session), typeId, out spec);
            var duration = hasSpec && spec.ConstructionDuration > 0.001f
                ? spec.ConstructionDuration
                : RoadMath.DefaultSectionSeconds;

            for (var i = 1; i < corners.Length; i++)
            {
                RoadSections.Spawn(
                    em, session, ref grid, ref network, typeId,
                    corners[i - 1], corners[i], duration, 1, false, 0);
            }

            corners.Dispose();
        }

        static bool TryPay(EntityManager em, Entity session, in FixedString64Bytes typeId, int sections)
        {
            if (!em.HasBuffer<BuildingCatalogCost>(session) || !em.HasBuffer<ResourceAmount>(session))
                return true;
            var costs = em.GetBuffer<BuildingCatalogCost>(session);
            var stock = em.GetBuffer<ResourceAmount>(session);
            if (!BuildingCosts.CanAffordScaled(costs, typeId, stock, sections))
                return false;
            BuildingCosts.PayScaled(costs, typeId, stock, sections);
            return true;
        }

        static void Reject(EntityManager em, Entity session, byte reason)
        {
            if (!em.HasBuffer<BuildingRejectedEvent>(session))
                return;
            em.GetBuffer<BuildingRejectedEvent>(session).Add(new BuildingRejectedEvent
            {
                Reason = reason
            });
        }
    }
}
