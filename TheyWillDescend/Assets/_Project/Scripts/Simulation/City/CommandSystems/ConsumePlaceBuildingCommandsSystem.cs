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

            var grid = em.GetComponentData<CityGrid>(session);
            DrainPendingScenario(em, session, ref grid);
            DrainCommands(em, session, ref grid);
            em.SetComponentData(session, grid);
        }

        static bool IsRunPrepared(EntityManager em, Entity session)
        {
            return em.HasComponent<SimControl>(session)
                && em.GetComponentData<SimControl>(session).RunPrepared != 0;
        }

        static void DrainPendingScenario(EntityManager em, Entity session, ref CityGrid grid)
        {
            if (!IsRunPrepared(em, session))
                return;
            if (!em.HasBuffer<PendingScenarioPlace>(session))
                return;

            var pending = em.GetBuffer<PendingScenarioPlace>(session);
            if (pending.Length == 0)
                return;

            if (!em.HasBuffer<BuildingPrototype>(session))
            {
                pending.Clear();
                return;
            }

            var copy = pending.ToNativeArray(Allocator.Temp);
            pending.Clear();
            for (var i = 0; i < copy.Length; i++)
            {
                var catalog = em.GetBuffer<BuildingPrototype>(session);
                var place = copy[i];
                Place(em, session, ref grid, catalog, new PlaceBuildingCommand
                {
                    TypeId = place.TypeId,
                    AnchorCluster = place.Cluster,
                    AnchorRadial = place.Radial,
                    InstantComplete = 1
                });
            }

            copy.Dispose();
        }

        static void DrainCommands(EntityManager em, Entity session, ref CityGrid grid)
        {
            if (!IsRunPrepared(em, session))
                return;
            if (!em.HasBuffer<PlaceBuildingCommand>(session))
                return;

            var commands = em.GetBuffer<PlaceBuildingCommand>(session);
            if (commands.Length == 0)
                return;

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
        }

        static void Place(
            EntityManager em,
            Entity session,
            ref CityGrid grid,
            DynamicBuffer<BuildingPrototype> catalog,
            in PlaceBuildingCommand command)
        {
            if (!BuildingCatalog.TryResolve(catalog, command.TypeId, out var spec))
            {
                Reject(em, session, command, BuildingRejectedEvent.UnknownType);
                return;
            }

            var footprint = spec.Footprint;
            var clusters = new NativeList<OccupiedCell>(64, Allocator.Temp);
            if (grid.Ready == 0
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

            if (!TryPay(em, session, spec.TypeId, command))
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

            var meshSize = spec.MeshSize > 0.001f ? spec.MeshSize : 1f;
            var scale = stubWorldSize / meshSize;

            var id = command.BuildingId > 0 ? command.BuildingId : grid.NextBuildingId + 1;
            if (grid.NextBuildingId < id)
                grid.NextBuildingId = id;

            var building = new Building
            {
                Id = id,
                TypeId = spec.TypeId,
                WidthClusters = footprint.WidthClusters,
                DepthRadialRings = footprint.DepthRadialRings,
                AnchorCluster = command.AnchorCluster,
                AnchorRadial = command.AnchorRadial
            };
            var transform = LocalTransform.FromPositionRotationScale(position, rotation, scale);
            var duration = command.ConstructionDuration > 0.001f
                ? command.ConstructionDuration
                : spec.ConstructionDuration;

            Construction? construction = null;
            if (command.InstantComplete == 0 && duration > 0.001f)
            {
                var site = new Construction
                {
                    Elapsed = math.max(0f, command.ConstructionElapsed),
                    Duration = duration
                };
                if (!site.IsComplete)
                    construction = site;
            }

            SpawnHouse(em, session, spec, building, transform, construction);
        }

        public static void SpawnHouse(
            EntityManager em,
            Entity session,
            in BuildingPrototype spec,
            in Building building,
            LocalTransform transform,
            Construction? construction)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, building);
            em.AddComponentData(entity, spec.ToBuildingType());
            em.AddComponentData(entity, new BuildingMeshSize
            {
                Horizontal = spec.MeshSize > 0.001f ? spec.MeshSize : 1f
            });
            if (spec.WorkplaceSlots > 0)
                em.AddComponentData(entity, new Workplace());
            CopyRecipes(em, session, spec.TypeId, entity);
            SimEntityPose.Apply(em, entity, transform);
            if (construction.HasValue)
                em.AddComponentData(entity, construction.Value);
#if UNITY_EDITOR
            em.SetName(entity, construction.HasValue
                ? $"BuildingSite_{building.Id}"
                : $"Building_{building.Id}");
#endif
        }

        static void CopyRecipes(
            EntityManager em,
            Entity session,
            in FixedString64Bytes typeId,
            Entity entity)
        {
            if (!em.HasBuffer<BuildingCatalogRecipe>(session))
                return;

            var src = em.GetBuffer<BuildingCatalogRecipe>(session);
            var lines = new NativeList<BuildingRecipeLine>(8, Allocator.Temp);
            for (var i = 0; i < src.Length; i++)
            {
                var row = src[i];
                if (row.TypeId != typeId || row.PerHour <= 0.0001f)
                    continue;
                lines.Add(new BuildingRecipeLine
                {
                    Kind = row.Kind,
                    ResourceId = row.ResourceId,
                    PerHour = row.PerHour
                });
            }

            if (lines.Length == 0)
            {
                lines.Dispose();
                return;
            }

            var dest = em.AddBuffer<BuildingRecipeLine>(entity);
            for (var i = 0; i < lines.Length; i++)
                dest.Add(lines[i]);
            lines.Dispose();
        }

        static bool TryPay(
            EntityManager em,
            Entity session,
            in FixedString64Bytes typeId,
            in PlaceBuildingCommand command)
        {
            if (command.InstantComplete != 0 || command.BuildingId > 0)
                return true;
            if (!em.HasBuffer<BuildingCatalogCost>(session))
                return true;

            var costs = em.GetBuffer<BuildingCatalogCost>(session);
            if (!em.HasBuffer<ResourceAmount>(session))
                return !BuildingCosts.HasCost(costs, typeId);

            var stock = em.GetBuffer<ResourceAmount>(session);
            if (!BuildingCosts.CanAfford(costs, typeId, stock))
                return false;

            BuildingCosts.Pay(costs, typeId, stock);
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
