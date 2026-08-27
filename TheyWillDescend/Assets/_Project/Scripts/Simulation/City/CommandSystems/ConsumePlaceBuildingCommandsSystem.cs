using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.City
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumeSpawnAgentCommandsSystem))]
    public partial struct ConsumePlaceBuildingCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimBridge>();
            state.RequireForUpdate<SimPrototypes>();
            state.RequireForUpdate<CityGrid>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Run(state.EntityManager);
        }

        public static void Run(EntityManager em)
        {
            if (!SimBridgeAccess.TryGet(em, out var session))
                return;

            var commands = em.GetBuffer<PlaceBuildingCommand>(session);
            if (commands.Length == 0)
                return;

            var grid = em.GetComponentData<CityGrid>(session);
            if (!em.HasBuffer<BuildingPrototype>(session))
            {
                commands.Clear();
                return;
            }

            var copy = commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            for (var i = 0; i < copy.Length; i++)
            {
                var catalog = em.GetBuffer<BuildingPrototype>(session);
                Place(em, session, ref grid, catalog, copy[i]);
            }
            copy.Dispose();
            em.SetComponentData(session, grid);
        }

        static void Place(
            EntityManager em,
            Entity session,
            ref CityGrid grid,
            DynamicBuffer<BuildingPrototype> catalog,
            in PlaceBuildingCommand command)
        {
            if (!BuildingCatalog.TryResolve(
                    catalog, command.TypeId, command.WidthClusters, command.DepthRadialRings, out var prototype))
            {
                Reject(em, session, command, BuildingRejectedEvent.UnknownType);
                return;
            }

            var footprint = prototype.Footprint;
            var prefab = prototype.Prefab;
            var clusters = new NativeList<OccupiedCell>(64, Allocator.Temp);
            if (grid.Ready == 0
                || prefab == Entity.Null
                || !RadialFootprintMath.TryExpandClusters(
                    grid.Config, command.AnchorCluster, command.AnchorRadial, footprint, clusters))
            {
                Reject(em, session, command, BuildingRejectedEvent.InvalidCell);
                clusters.Dispose();
                return;
            }

            var occupied = em.GetBuffer<OccupiedCell>(session);
            if (Overlaps(occupied, clusters))
            {
                Reject(em, session, command, BuildingRejectedEvent.Overlap);
                clusters.Dispose();
                return;
            }

            if (!TryPay(em, session, prototype.TypeId, command))
            {
                Reject(em, session, command, BuildingRejectedEvent.Unaffordable);
                clusters.Dispose();
                return;
            }

            for (var i = 0; i < clusters.Length; i++)
                occupied.Add(clusters[i]);
            clusters.Dispose();

            RadialFootprintMath.FootprintMarkerPose(
                grid.Center, grid.Config, command.AnchorCluster, command.AnchorRadial, footprint,
                out var position, out var rotation, out var stubWorldSize);

            var meshSize = prototype.MeshSize;
            var scale = meshSize > 0.001f ? stubWorldSize / meshSize : 1f;

            var id = command.BuildingId > 0 ? command.BuildingId : grid.NextBuildingId + 1;
            if (grid.NextBuildingId < id)
                grid.NextBuildingId = id;

            var building = new Building
            {
                Id = id,
                TypeId = prototype.TypeId,
                WidthClusters = footprint.WidthClusters,
                DepthRadialRings = footprint.DepthRadialRings,
                AnchorCluster = command.AnchorCluster,
                AnchorRadial = command.AnchorRadial
            };
            var transform = LocalTransform.FromPositionRotationScale(position, rotation, scale);
            var duration = command.ConstructionDuration > 0.001f
                ? command.ConstructionDuration
                : (prototype.ConstructionDuration > 0.001f
                    ? prototype.ConstructionDuration
                    : (grid.ConstructionDuration > 0.001f ? grid.ConstructionDuration : 8f));

            if (command.InstantComplete != 0)
            {
                SpawnFinishedHouse(em, prefab, building, transform);
                return;
            }

            var construction = new Construction
            {
                Elapsed = math.max(0f, command.ConstructionElapsed),
                Duration = duration
            };
            if (construction.IsComplete)
            {
                SpawnFinishedHouse(em, prefab, building, transform);
                return;
            }

            var site = em.CreateEntity();
            em.AddComponentData(site, building);
            em.AddComponentData(site, construction);
            em.AddComponentData(site, transform);
            if (prototype.WorkplaceSlots > 0)
                em.AddComponentData(site, new Workplace());
#if UNITY_EDITOR
            em.SetName(site, $"BuildingSite_{building.Id}");
#endif
        }

        public static void SpawnFinishedHouse(
            EntityManager em,
            Entity prefab,
            in Building building,
            LocalTransform transform)
        {
            var entity = em.Instantiate(prefab);
            if (!em.HasComponent<Building>(entity))
                em.AddComponent<Building>(entity);
            em.SetComponentData(entity, building);
            if (!em.HasComponent<Workplace>(entity))
                em.AddComponent<Workplace>(entity);
            em.SetComponentData(entity, new Workplace());
            SimEntityPose.Apply(em, entity, transform);
#if UNITY_EDITOR
            em.SetName(entity, $"Building_{building.Id}");
#endif
        }

        public static Entity ResolveHousePrefab(
            DynamicBuffer<BuildingPrototype> catalog,
            in FixedString64Bytes typeId,
            int widthClusters,
            int depthRadialRings)
        {
            return BuildingCatalog.TryResolve(catalog, typeId, widthClusters, depthRadialRings, out var prototype)
                ? prototype.Prefab
                : Entity.Null;
        }

        static bool TryPay(
            EntityManager em,
            Entity session,
            in FixedString64Bytes typeId,
            in PlaceBuildingCommand command)
        {
            if (command.InstantComplete != 0 || command.BuildingId > 0)
                return true;
            if (!em.HasBuffer<BuildingCost>(session))
                return true;

            var costs = em.GetBuffer<BuildingCost>(session);
            if (!em.HasBuffer<ResourceAmount>(session))
            {
                for (var i = 0; i < costs.Length; i++)
                {
                    if (costs[i].TypeId == typeId && costs[i].Amount > 0.0001f)
                        return false;
                }

                return true;
            }

            var stock = em.GetBuffer<ResourceAmount>(session);
            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i];
                if (cost.TypeId != typeId || cost.Amount <= 0.0001f)
                    continue;
                if (!ResourceLedger.Has(stock, cost.ResourceId, cost.Amount))
                    return false;
            }

            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i];
                if (cost.TypeId != typeId || cost.Amount <= 0.0001f)
                    continue;
                ResourceLedger.Add(stock, cost.ResourceId, -cost.Amount);
            }

            return true;
        }

        static void Reject(EntityManager em, Entity session, in PlaceBuildingCommand command, byte reason)
        {
            em.GetBuffer<BuildingRejectedEvent>(session).Add(new BuildingRejectedEvent
            {
                AnchorCluster = command.AnchorCluster,
                AnchorRadial = command.AnchorRadial,
                Reason = reason
            });
        }

        static bool Overlaps(DynamicBuffer<OccupiedCell> occupied, NativeList<OccupiedCell> clusters)
        {
            for (var i = 0; i < clusters.Length; i++)
            {
                var cell = clusters[i];
                for (var j = 0; j < occupied.Length; j++)
                {
                    if (occupied[j].Cluster == cell.Cluster && occupied[j].Radial == cell.Radial)
                        return true;
                }
            }

            return false;
        }
    }
}
