using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Shell;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Io;
using TheyWillDescend.Simulation.Time;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.App
{
    /// <summary>
    /// Maps ECS write model ↔ slot DTO. Does not own GameObjects.
    /// </summary>
    public static class RunSessionSnapshot
    {
        public const int PayloadVersion = 6;

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
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<CircleWalk>(),
                ComponentType.ReadOnly<AgentType>());
            var transforms = agentQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var walks = agentQuery.ToComponentDataArray<CircleWalk>(Allocator.Temp);
            var types = agentQuery.ToComponentDataArray<AgentType>(Allocator.Temp);
            snapshot.agents = new AgentSnapshot[transforms.Length];
            for (var i = 0; i < transforms.Length; i++)
            {
                var transform = transforms[i];
                var walk = walks[i];
                var forward = math.mul(transform.Rotation, new float3(0f, 0f, 1f));
                snapshot.agents[i] = new AgentSnapshot
                {
                    agentType = (byte)types[i].Kind,
                    posX = transform.Position.x,
                    posY = transform.Position.y,
                    posZ = transform.Position.z,
                    fwdX = forward.x,
                    fwdY = forward.y,
                    fwdZ = forward.z,
                    centerX = walk.Center.x,
                    centerY = walk.Center.y,
                    centerZ = walk.Center.z,
                    radius = walk.Radius,
                    speed = walk.Speed,
                    direction = walk.Direction,
                    angleRadians = walk.AngleRadians
                };
            }

            transforms.Dispose();
            walks.Dispose();
            types.Dispose();

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
            SimIo.PlaybackCommands();

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

            SimIo.PlaybackCommands();
            GameLog.Info(
                $"Applied snapshot: day {snapshot.day}, agents {snapshot.agents?.Length ?? 0}, buildings {snapshot.buildings?.Length ?? 0}.");
        }

        static void EnqueueAgent(AgentSnapshot record, int version)
        {
            var walk = new CircleWalk
            {
                Center = new float3(record.centerX, record.centerY, record.centerZ),
                Radius = record.radius,
                Speed = record.speed,
                Direction = record.direction,
                AngleRadians = record.angleRadians
            };
            float3 position;
            float3 facing;
            if (version >= 2)
            {
                position = new float3(record.posX, record.posY, record.posZ);
                facing = new float3(record.fwdX, record.fwdY, record.fwdZ);
            }
            else
                walk.GetPose(out position, out facing);

            SimIo.TryEnqueueSpawn(new SpawnAgentCommand
            {
                Center = walk.Center,
                Radius = walk.Radius,
                Speed = walk.Speed,
                Direction = walk.Direction,
                AngleRadians = walk.AngleRadians,
                Position = position,
                Facing = facing,
                HasPose = 1,
                Kind = (AgentKind)record.agentType
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
