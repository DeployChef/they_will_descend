using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Io;
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
            var catalog = em.GetComponentData<SimPrototypes>(session);
            var copy = commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            for (var i = 0; i < copy.Length; i++)
                Place(em, session, ref grid, catalog, copy[i]);
            copy.Dispose();
            em.SetComponentData(session, grid);
        }

        static void Place(
            EntityManager em,
            Entity session,
            ref CityGrid grid,
            in SimPrototypes catalog,
            in PlaceBuildingCommand command)
        {
            var footprint = new BuildingFootprint
            {
                WidthClusters = command.WidthClusters,
                DepthRadialRings = command.DepthRadialRings
            };
            var prefab = ResolveHousePrefab(catalog, command.WidthClusters, command.DepthRadialRings);

            var clusters = new NativeList<OccupiedCell>(64, Allocator.Temp);
            if (grid.Ready == 0
                || prefab == Entity.Null
                || !RadialFootprintMath.TryExpandClusters(
                    grid.Config, command.AnchorCluster, command.AnchorRadial, footprint, clusters))
            {
                Reject(em, session, command);
                clusters.Dispose();
                return;
            }

            var occupied = em.GetBuffer<OccupiedCell>(session);
            if (Overlaps(occupied, clusters))
            {
                Reject(em, session, command);
                clusters.Dispose();
                return;
            }

            for (var i = 0; i < clusters.Length; i++)
                occupied.Add(clusters[i]);
            clusters.Dispose();

            RadialFootprintMath.FootprintMarkerPose(
                grid.Center, grid.Config, command.AnchorCluster, command.AnchorRadial, footprint,
                out var position, out var rotation, out var stubWorldSize);

            var isSmall = footprint.WidthClusters == 2 && footprint.DepthRadialRings == 2;
            var meshSize = isSmall ? catalog.House2x2MeshSize : catalog.House6x2MeshSize;
            if (meshSize <= 0.001f)
                meshSize = isSmall ? catalog.House6x2MeshSize : catalog.House2x2MeshSize;
            var scale = meshSize > 0.001f ? stubWorldSize / meshSize : 1f;

            var id = command.BuildingId > 0 ? command.BuildingId : grid.NextBuildingId + 1;
            if (grid.NextBuildingId < id)
                grid.NextBuildingId = id;

            var building = new Building
            {
                Id = id,
                WidthClusters = command.WidthClusters,
                DepthRadialRings = command.DepthRadialRings,
                AnchorCluster = command.AnchorCluster,
                AnchorRadial = command.AnchorRadial
            };
            var transform = LocalTransform.FromPositionRotationScale(position, rotation, scale);
            var duration = command.ConstructionDuration > 0.001f
                ? command.ConstructionDuration
                : (grid.ConstructionDuration > 0.001f ? grid.ConstructionDuration : 8f);

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
            SimEntityPose.Apply(em, entity, transform);
#if UNITY_EDITOR
            em.SetName(entity, $"Building_{building.Id}");
#endif
        }

        public static Entity ResolveHousePrefab(
            in SimPrototypes catalog,
            int widthClusters,
            int depthRadialRings)
        {
            var isSmall = widthClusters == 2 && depthRadialRings == 2;
            var prefab = isSmall ? catalog.House2x2 : catalog.House6x2;
            if (prefab == Entity.Null)
                prefab = catalog.House6x2 != Entity.Null ? catalog.House6x2 : catalog.House2x2;
            return prefab;
        }

        static void Reject(EntityManager em, Entity session, in PlaceBuildingCommand command)
        {
            em.GetBuffer<BuildingRejectedEvent>(session).Add(new BuildingRejectedEvent
            {
                AnchorCluster = command.AnchorCluster,
                AnchorRadial = command.AnchorRadial
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
