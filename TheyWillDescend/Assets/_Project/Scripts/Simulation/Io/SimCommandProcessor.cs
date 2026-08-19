using System.Collections.Generic;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// Applies command buffers. Shared by the sim tick and load (same-frame flush).
    /// </summary>
    public static class SimCommandProcessor
    {
        public static void Run(EntityManager em)
        {
            var bridgeEntity = SimBridgeAccess.GetOrCreate(em);
            var gridEntity = SimBridgeAccess.GetOrCreateCityGrid(em);
            var bridge = em.GetComponentData<SimBridge>(bridgeEntity);

            if (bridge.DespawnAllAgents != 0)
            {
                using var agents = em.CreateEntityQuery(ComponentType.ReadOnly<AgentId>());
                if (!agents.IsEmptyIgnoreFilter)
                {
                    using var ids = agents.ToComponentDataArray<AgentId>(Allocator.Temp);
                    var despawned = em.GetBuffer<AgentDespawnedEvent>(bridgeEntity);
                    for (var i = 0; i < ids.Length; i++)
                        despawned.Add(new AgentDespawnedEvent { AgentId = ids[i].Value });
                    em.DestroyEntity(agents);
                }

                bridge.DespawnAllAgents = 0;
            }

            if (bridge.DespawnAllBuildings != 0)
            {
                using var buildings = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
                if (!buildings.IsEmptyIgnoreFilter)
                {
                    using var data = buildings.ToComponentDataArray<Building>(Allocator.Temp);
                    var despawned = em.GetBuffer<BuildingDespawnedEvent>(bridgeEntity);
                    for (var i = 0; i < data.Length; i++)
                        despawned.Add(new BuildingDespawnedEvent { BuildingId = data[i].Id });
                    em.DestroyEntity(buildings);
                }

                em.GetBuffer<OccupiedCell>(gridEntity).Clear();
                bridge.DespawnAllBuildings = 0;
            }

            var spawnCommands = em.GetBuffer<SpawnAgentCommand>(bridgeEntity);
            if (spawnCommands.Length > 0)
            {
                var copy = spawnCommands.ToNativeArray(Allocator.Temp);
                spawnCommands.Clear();
                for (var i = 0; i < copy.Length; i++)
                    SpawnAgent(em, bridgeEntity, ref bridge, copy[i]);
                copy.Dispose();
            }

            var placeCommands = em.GetBuffer<PlaceBuildingCommand>(bridgeEntity);
            if (placeCommands.Length > 0)
            {
                var copy = placeCommands.ToNativeArray(Allocator.Temp);
                placeCommands.Clear();
                var grid = em.GetComponentData<CityGrid>(gridEntity);
                for (var i = 0; i < copy.Length; i++)
                    PlaceBuilding(em, bridgeEntity, gridEntity, ref grid, copy[i]);
                em.SetComponentData(gridEntity, grid);
                copy.Dispose();
            }

            em.SetComponentData(bridgeEntity, bridge);
        }

        static void SpawnAgent(
            EntityManager em,
            Entity bridgeEntity,
            ref SimBridge bridge,
            in SpawnAgentCommand command)
        {
            bridge.NextAgentId += 1;
            var id = bridge.NextAgentId;
            var walk = new CircleWalk
            {
                Center = command.Center,
                Radius = command.Radius,
                Speed = command.Speed,
                Direction = command.Direction,
                AngleRadians = command.AngleRadians
            };
            var position = command.HasPose != 0
                ? new AgentPosition { Value = command.Position, Facing = command.Facing }
                : walk.ToPosition();

            var entity = em.CreateEntity();
            em.AddComponentData(entity, new AgentId { Value = id });
            em.AddComponentData(entity, new AgentVisualId { Value = command.VisualId });
            em.AddComponentData(entity, walk);
            em.AddComponentData(entity, position);
#if UNITY_EDITOR
            em.SetName(entity, $"Agent_{id}");
#endif
            em.GetBuffer<AgentSpawnedEvent>(bridgeEntity).Add(new AgentSpawnedEvent
            {
                AgentId = id,
                Position = position.Value,
                VisualId = command.VisualId
            });
        }

        static void PlaceBuilding(
            EntityManager em,
            Entity bridgeEntity,
            Entity gridEntity,
            ref CityGrid grid,
            in PlaceBuildingCommand command)
        {
            var clusters = new List<(int cluster, int radial)>(64);
            var footprint = new BuildingFootprint
            {
                WidthClusters = command.WidthClusters,
                DepthRadialRings = command.DepthRadialRings
            };
            if (grid.Ready == 0
                || !RadialFootprintMath.TryExpandClusters(
                    grid.Config, command.AnchorCluster, command.AnchorRadial, footprint, clusters))
            {
                em.GetBuffer<BuildingRejectedEvent>(bridgeEntity).Add(new BuildingRejectedEvent
                {
                    AnchorCluster = command.AnchorCluster,
                    AnchorRadial = command.AnchorRadial
                });
                return;
            }

            var occupied = em.GetBuffer<OccupiedCell>(gridEntity);
            if (Overlaps(occupied, clusters))
            {
                em.GetBuffer<BuildingRejectedEvent>(bridgeEntity).Add(new BuildingRejectedEvent
                {
                    AnchorCluster = command.AnchorCluster,
                    AnchorRadial = command.AnchorRadial
                });
                return;
            }

            for (var i = 0; i < clusters.Count; i++)
            {
                occupied.Add(new OccupiedCell
                {
                    Cluster = clusters[i].cluster,
                    Radial = clusters[i].radial
                });
            }

            grid.NextBuildingId += 1;
            var id = grid.NextBuildingId;
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new Building
            {
                Id = id,
                WidthClusters = command.WidthClusters,
                DepthRadialRings = command.DepthRadialRings,
                AnchorCluster = command.AnchorCluster,
                AnchorRadial = command.AnchorRadial
            });
#if UNITY_EDITOR
            em.SetName(entity, $"Building_{id}");
#endif
            em.GetBuffer<BuildingPlacedEvent>(bridgeEntity).Add(new BuildingPlacedEvent
            {
                BuildingId = id,
                WidthClusters = command.WidthClusters,
                DepthRadialRings = command.DepthRadialRings,
                AnchorCluster = command.AnchorCluster,
                AnchorRadial = command.AnchorRadial
            });
        }

        static bool Overlaps(DynamicBuffer<OccupiedCell> occupied, List<(int cluster, int radial)> clusters)
        {
            for (var i = 0; i < clusters.Count; i++)
            {
                var cell = clusters[i];
                for (var j = 0; j < occupied.Length; j++)
                {
                    if (occupied[j].Cluster == cell.cluster && occupied[j].Radial == cell.radial)
                        return true;
                }
            }

            return false;
        }
    }
}
