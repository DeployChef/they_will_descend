using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Research;
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
            state.RequireForUpdate<SimSession>();
            state.RequireForUpdate<CityGrid>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SimSessionAccess.TryGet(em, out var session))
                return;

            var grid = em.GetComponentData<CityGrid>(session);
            DrainPendingScenario(em, session, ref grid);
            DrainRequests(ref state, em, session, ref grid);
            em.SetComponentData(session, grid);
        }

        static void DrainPendingScenario(EntityManager em, Entity session, ref CityGrid grid)
        {
            if (!em.GetComponentData<SimSession>(session).AcceptsSetupCommands)
                return;
            if (!em.HasBuffer<PendingScenarioPlace>(session))
                return;

            var pending = em.GetBuffer<PendingScenarioPlace>(session);
            if (pending.Length == 0)
                return;

            if (!em.HasBuffer<BuildingPrototype>(session))
                return;

            var copy = pending.ToNativeArray(Allocator.Temp);
            pending.Clear();
            for (var i = 0; i < copy.Length; i++)
            {
                var place = copy[i];
                Place(em, session, ref grid, new PlaceBuildingRequest
                {
                    TypeId = place.TypeId,
                    AnchorCluster = place.Cluster,
                    AnchorRadial = place.Radial,
                    InstantComplete = 1,
                    Source = PlaceBuildingCommandSource.Setup
                });
            }

            copy.Dispose();
        }

        void DrainRequests(ref SystemState state, EntityManager em, Entity session, ref CityGrid grid)
        {
            var query = SystemAPI.QueryBuilder().WithAll<PlaceBuildingRequest>().Build();
            if (query.IsEmptyIgnoreFilter)
                return;

            if (!em.HasBuffer<BuildingPrototype>(session))
                return;

            var lifecycle = em.GetComponentData<SimSession>(session);
            using var requestEntities = query.ToEntityArray(Allocator.Temp);
            using var requests = query.ToComponentDataArray<PlaceBuildingRequest>(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                var sourceAllowed = lifecycle.IsReady
                    ? request.Source == PlaceBuildingCommandSource.Gameplay
                    : lifecycle.AcceptsSetupCommands
                        && request.Source == PlaceBuildingCommandSource.SnapshotRestore;

                if (sourceAllowed)
                {
                    Place(em, session, ref grid, in request);
                }

                em.DestroyEntity(requestEntities[i]);
            }
        }


        static void Place(
            EntityManager em,
            Entity session,
            ref CityGrid grid,
            in PlaceBuildingRequest command)
        {
            if (!em.HasBuffer<BuildingPrototype>(session))
                return;

            var catalog = em.GetBuffer<BuildingPrototype>(session);
            if (!BuildingCatalog.TryResolve(catalog, command.TypeId, out var spec))
            {
                Reject(em, session, command, BuildingRejectedEvent.UnknownType);
                return;
            }


            if (spec.RequiresUnlock != 0
                && command.Source == PlaceBuildingCommandSource.Gameplay
                && !ResearchRules.IsBuildingUnlocked(em, spec.TypeId))
            {
                Reject(em, session, command, BuildingRejectedEvent.Locked);
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
                out var position, out var rotation);

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
            var transform = LocalTransform.FromPositionRotationScale(position, rotation, 1f);
            var duration = command.ConstructionDuration > 0.001f
                ? command.ConstructionDuration
                : spec.ConstructionDuration;

            Construction? construction = null;
            if (command.InstantComplete == 0)
            {
                var site = new Construction
                {
                    Elapsed = math.max(0f, command.ConstructionElapsed),
                    Duration = duration,
                    Dismantling = command.Dismantling
                };
                if (site.IsDismantling && site.Elapsed <= 0.0001f && duration > 0.001f)
                    site.Elapsed = duration;
                if (!site.IsComplete)
                    construction = site;
            }

            var desired = command.Source == PlaceBuildingCommandSource.SnapshotRestore
                ? command.DesiredWorkers
                : 0;
            var paused = command.Source == PlaceBuildingCommandSource.SnapshotRestore
                ? command.Paused
                : (byte)0;
            SpawnHouse(em, session, spec, building, transform, construction, desired, paused);
        }

        public static void SpawnHouse(
            EntityManager em,
            Entity session,
            in BuildingPrototype spec,
            in Building building,
            LocalTransform transform,
            Construction? construction,
            int desiredWorkers = 0,
            byte paused = 0)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, building);
            em.AddComponentData(entity, spec.ToBuildingType());
            if (spec.WorkplaceSlots > 0)
            {
                em.AddComponentData(entity, new Workplace
                {
                    DesiredWorkers = desiredWorkers,
                    Paused = paused
                });
            }




            if (spec.ResearchWorkplace != 0)
                em.AddComponentData(entity, new ResearchWorkplace());
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
            in PlaceBuildingRequest command)
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

        static void Reject(EntityManager em, Entity session, in PlaceBuildingRequest command, byte reason)

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
