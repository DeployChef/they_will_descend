using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// Applies command buffers on the sim tick, and once more during load playback.
    /// Agents/houses are Instantiate of baked entity stamps, not AddComponent soup.
    /// </summary>
    public static class SimCommandProcessor
    {
        public static void Run(EntityManager em)
        {
            if (!SimBridgeAccess.TryGet(em, out var session))
                return;

            var bridge = em.GetComponentData<SimBridge>(session);
            var grid = em.GetComponentData<CityGrid>(session);
            var catalog = em.HasComponent<SimPrototypes>(session)
                ? em.GetComponentData<SimPrototypes>(session)
                : default;

            if (bridge.DespawnAllAgents != 0)
            {
                using var agents = em.CreateEntityQuery(
                    ComponentType.ReadOnly<AgentId>(),
                    ComponentType.ReadOnly<CircleWalk>());
                DestroyQuery(em, agents);
                bridge.DespawnAllAgents = 0;
            }

            if (bridge.DespawnAllBuildings != 0)
            {
                using var buildings = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
                DestroyQuery(em, buildings);
                em.GetBuffer<OccupiedCell>(session).Clear();
                bridge.DespawnAllBuildings = 0;
            }

            var spawnCommands = em.GetBuffer<SpawnAgentCommand>(session);
            if (spawnCommands.Length > 0)
            {
                var copy = spawnCommands.ToNativeArray(Allocator.Temp);
                spawnCommands.Clear();
                for (var i = 0; i < copy.Length; i++)
                    SpawnAgent(em, ref bridge, catalog, copy[i]);
                copy.Dispose();
            }

            var placeCommands = em.GetBuffer<PlaceBuildingCommand>(session);
            if (placeCommands.Length > 0)
            {
                var copy = placeCommands.ToNativeArray(Allocator.Temp);
                placeCommands.Clear();
                for (var i = 0; i < copy.Length; i++)
                    PlaceBuilding(em, session, ref grid, catalog, copy[i]);
                copy.Dispose();
            }

            em.SetComponentData(session, bridge);
            em.SetComponentData(session, grid);
        }

        static void SpawnAgent(
            EntityManager em,
            ref SimBridge bridge,
            in SimPrototypes catalog,
            in SpawnAgentCommand command)
        {
            if (catalog.Agent == Entity.Null)
                return;

            bridge.NextAgentId += 1;
            var walk = new CircleWalk
            {
                Center = command.Center,
                Radius = command.Radius,
                Speed = command.Speed,
                Direction = command.Direction,
                AngleRadians = command.AngleRadians
            };
            var transform = command.HasPose != 0
                ? LocalTransform.FromPositionRotation(
                    command.Position,
                    quaternion.LookRotationSafe(command.Facing, math.up()))
                : walk.ToLocalTransform();

            var entity = em.Instantiate(catalog.Agent);
            em.SetComponentData(entity, new AgentId { Value = bridge.NextAgentId });
            em.SetComponentData(entity, new AgentType { Kind = command.Kind });
            em.SetComponentData(entity, walk);
            SetLocalTransform(em, entity, transform);
#if UNITY_EDITOR
            em.SetName(entity, $"Agent_{bridge.NextAgentId}");
#endif
        }

        static void PlaceBuilding(
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
            var isSmall = footprint.WidthClusters == 2 && footprint.DepthRadialRings == 2;
            var prefab = isSmall ? catalog.House2x2 : catalog.House6x2;
            if (prefab == Entity.Null)
                prefab = catalog.House6x2 != Entity.Null ? catalog.House6x2 : catalog.House2x2;

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

            var meshSize = isSmall ? catalog.House2x2MeshSize : catalog.House6x2MeshSize;
            if (meshSize <= 0.001f)
                meshSize = isSmall ? catalog.House6x2MeshSize : catalog.House2x2MeshSize;
            var scale = meshSize > 0.001f ? stubWorldSize / meshSize : 1f;

            grid.NextBuildingId += 1;
            var entity = em.Instantiate(prefab);
            if (!em.HasComponent<Building>(entity))
                em.AddComponent<Building>(entity);
            em.SetComponentData(entity, new Building
            {
                Id = grid.NextBuildingId,
                WidthClusters = command.WidthClusters,
                DepthRadialRings = command.DepthRadialRings,
                AnchorCluster = command.AnchorCluster,
                AnchorRadial = command.AnchorRadial
            });
            SetLocalTransform(
                em,
                entity,
                LocalTransform.FromPositionRotationScale(position, rotation, scale));
#if UNITY_EDITOR
            em.SetName(entity, $"Building_{grid.NextBuildingId}");
#endif
        }

        static void SetLocalTransform(EntityManager em, Entity entity, LocalTransform transform)
        {
            if (!em.HasComponent<LocalTransform>(entity))
                em.AddComponent<LocalTransform>(entity);
            em.SetComponentData(entity, transform);
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

        static void DestroyQuery(EntityManager em, EntityQuery query)
        {
            if (query.IsEmptyIgnoreFilter)
                return;

            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.Exists(entity))
                    continue;
                if (em.HasBuffer<LinkedEntityGroup>(entity))
                {
                    var group = em.GetBuffer<LinkedEntityGroup>(entity)
                        .ToNativeArray(Allocator.Temp);
                    em.DestroyEntity(group.Reinterpret<Entity>());
                    group.Dispose();
                }
                else
                    em.DestroyEntity(entity);
            }
        }
    }
}
