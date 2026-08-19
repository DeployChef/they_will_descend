using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Shell;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Io;
using TheyWillDescend.Simulation.Time;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.App
{
    /// <summary>
    /// Maps ECS write model ↔ slot DTO. Does not own GameObjects.
    /// </summary>
    public static class RunSessionSnapshot
    {
        public const int PayloadVersion = 4;

        public static RunSnapshot Capture()
        {
            var snapshot = new RunSnapshot { version = PayloadVersion };
            var gate = SimGate.Active;
            if (gate != null)
            {
                snapshot.speed = gate.Speed;
                snapshot.playerPaused = gate.PlayerPaused;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return snapshot;

            var em = world.EntityManager;
            using var timeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<GameTime>());
            if (!timeQuery.IsEmptyIgnoreFilter)
            {
                var time = timeQuery.GetSingleton<GameTime>();
                snapshot.day = time.Day;
                snapshot.elapsedInDay = time.ElapsedInDay;
                snapshot.dayDuration = time.DayDuration;
            }

            using var agentQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<AgentPosition>(),
                ComponentType.ReadOnly<CircleWalk>(),
                ComponentType.ReadOnly<AgentVisualId>());
            var positions = agentQuery.ToComponentDataArray<AgentPosition>(Allocator.Temp);
            var walks = agentQuery.ToComponentDataArray<CircleWalk>(Allocator.Temp);
            var visuals = agentQuery.ToComponentDataArray<AgentVisualId>(Allocator.Temp);
            snapshot.agents = new AgentSnapshot[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                var position = positions[i];
                var walk = walks[i];
                snapshot.agents[i] = new AgentSnapshot
                {
                    prefabId = visuals[i].Value.ToString(),
                    posX = position.Value.x,
                    posY = position.Value.y,
                    posZ = position.Value.z,
                    fwdX = position.Facing.x,
                    fwdY = position.Facing.y,
                    fwdZ = position.Facing.z,
                    centerX = walk.Center.x,
                    centerY = walk.Center.y,
                    centerZ = walk.Center.z,
                    radius = walk.Radius,
                    speed = walk.Speed,
                    direction = walk.Direction,
                    angleRadians = walk.AngleRadians
                };
            }

            positions.Dispose();
            walks.Dispose();
            visuals.Dispose();

            using var buildingQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
            var buildings = buildingQuery.ToComponentDataArray<Building>(Allocator.Temp);
            snapshot.buildings = new BuildingSnapshot[buildings.Length];
            for (var i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                snapshot.buildings[i] = new BuildingSnapshot
                {
                    widthClusters = building.WidthClusters,
                    depthRadialRings = building.DepthRadialRings,
                    anchorCluster = building.AnchorCluster,
                    anchorRadial = building.AnchorRadial
                };
            }

            buildings.Dispose();
            return snapshot;
        }

        public static void Apply(RunSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
                world.EntityManager.CompleteAllTrackedJobs();

            SimGate.Active?.RestoreFromSnapshot(snapshot.speed, playerPaused: true);

            SimIo.TryRequestDespawnAllAgents();
            SimIo.TryRequestDespawnAllBuildings();
            SimIo.Flush();

            ApplyGameTime(snapshot);

            if (snapshot.agents != null)
            {
                for (var i = 0; i < snapshot.agents.Length; i++)
                    EnqueueAgent(snapshot.agents[i], snapshot.version);
            }

            if (snapshot.buildings != null)
            {
                for (var i = 0; i < snapshot.buildings.Length; i++)
                {
                    var record = snapshot.buildings[i];
                    SimIo.TryEnqueuePlaceBuilding(new PlaceBuildingCommand
                    {
                        WidthClusters = record.widthClusters,
                        DepthRadialRings = record.depthRadialRings,
                        AnchorCluster = record.anchorCluster,
                        AnchorRadial = record.anchorRadial
                    });
                }
            }

            SimIo.Flush();
            GameLog.Info(
                $"Applied snapshot: day {snapshot.day}, agents {snapshot.agents?.Length ?? 0}, buildings {snapshot.buildings?.Length ?? 0}.");
        }

        static void EnqueueAgent(AgentSnapshot record, int version)
        {
            var walk = new CircleWalk
            {
                Center = new Unity.Mathematics.float3(record.centerX, record.centerY, record.centerZ),
                Radius = record.radius,
                Speed = record.speed,
                Direction = record.direction,
                AngleRadians = record.angleRadians
            };
            var pose = version >= 2
                ? new AgentPosition
                {
                    Value = new Unity.Mathematics.float3(record.posX, record.posY, record.posZ),
                    Facing = new Unity.Mathematics.float3(record.fwdX, record.fwdY, record.fwdZ)
                }
                : walk.ToPosition();
            SimIo.TryEnqueueSpawn(new SpawnAgentCommand
            {
                Center = walk.Center,
                Radius = walk.Radius,
                Speed = walk.Speed,
                Direction = walk.Direction,
                AngleRadians = walk.AngleRadians,
                Position = pose.Value,
                Facing = pose.Facing,
                HasPose = 1,
                VisualId = string.IsNullOrEmpty(record.prefabId)
                    ? default
                    : new FixedString64Bytes(record.prefabId)
            });
        }

        static void ApplyGameTime(RunSnapshot snapshot)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;
            using var timeQuery = em.CreateEntityQuery(ComponentType.ReadWrite<GameTime>());
            if (timeQuery.IsEmptyIgnoreFilter)
                return;

            var entity = timeQuery.GetSingletonEntity();
            em.SetComponentData(entity, new GameTime
            {
                Day = snapshot.day,
                ElapsedInDay = snapshot.elapsedInDay,
                DayDuration = snapshot.dayDuration > 0f ? snapshot.dayDuration : 5f
            });
        }
    }
}
