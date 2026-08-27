using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
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
        public const int PayloadVersion = RunSnapshot.CurrentVersion;

        public static RunSnapshot Capture()
        {
            var snapshot = new RunSnapshot { version = PayloadVersion };
            if (!SimWorld.TryGet(out var em, out var bag))
                return snapshot;

            var control = em.GetComponentData<SimControl>(bag);
            snapshot.speed = control.Speed;
            snapshot.playerPaused = control.PlayerPaused != 0;

            using var timeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<GameTime>());
            if (!timeQuery.IsEmptyIgnoreFilter)
            {
                var time = timeQuery.GetSingleton<GameTime>();
                snapshot.day = time.Day;
                snapshot.elapsedInDay = time.ElapsedInDay;
                snapshot.dayDuration = time.DayDuration;
            }

            if (em.HasBuffer<ResourceAmount>(bag) && em.HasBuffer<ResourceInfo>(bag))
            {
                var stock = em.GetBuffer<ResourceAmount>(bag);
                var info = em.GetBuffer<ResourceInfo>(bag);
                snapshot.resources = new ResourceSnapshot[info.Length];
                for (var i = 0; i < info.Length; i++)
                {
                    var row = info[i];
                    snapshot.resources[i] = new ResourceSnapshot
                    {
                        resourceId = row.ResourceId.ToString(),
                        amount = ResourceLedger.Get(stock, row.ResourceId)
                    };
                }
            }

            using var agentQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<AgentLocomotion>(),
                ComponentType.ReadOnly<AgentType>(),
                ComponentType.ReadOnly<AgentId>(),
                ComponentType.ReadOnly<AgentAssignment>(),
                ComponentType.ReadOnly<AgentPlazaIdle>());
            var transforms = agentQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var motors = agentQuery.ToComponentDataArray<AgentLocomotion>(Allocator.Temp);
            var types = agentQuery.ToComponentDataArray<AgentType>(Allocator.Temp);
            var ids = agentQuery.ToComponentDataArray<AgentId>(Allocator.Temp);
            var assignments = agentQuery.ToComponentDataArray<AgentAssignment>(Allocator.Temp);
            var plazas = agentQuery.ToComponentDataArray<AgentPlazaIdle>(Allocator.Temp);
            snapshot.agents = new AgentSnapshot[transforms.Length];
            for (var i = 0; i < transforms.Length; i++)
            {
                var transform = transforms[i];
                var forward = math.mul(transform.Rotation, new float3(0f, 0f, 1f));
                snapshot.agents[i] = new AgentSnapshot
                {
                    agentType = (byte)types[i].Kind,
                    agentId = ids[i].Value,
                    posX = transform.Position.x,
                    posY = transform.Position.y,
                    posZ = transform.Position.z,
                    fwdX = forward.x,
                    fwdY = forward.y,
                    fwdZ = forward.z,
                    speed = motors[i].Speed,
                    targetX = motors[i].Target.x,
                    targetY = motors[i].Target.y,
                    targetZ = motors[i].Target.z,
                    moving = motors[i].Moving,
                    workplaceBuildingId = assignments[i].WorkplaceBuildingId,
                    arrived = assignments[i].Arrived,
                    plazaWalking = plazas[i].Walking,
                    plazaTimer = plazas[i].Timer,
                    plazaAngle = plazas[i].Angle,
                    plazaRadius = plazas[i].Radius
                };
            }

            transforms.Dispose();
            motors.Dispose();
            types.Dispose();
            ids.Dispose();
            assignments.Dispose();
            plazas.Dispose();

            using var buildingQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.Exclude<Headquarters>());
            var buildingEntities = buildingQuery.ToEntityArray(Allocator.Temp);
            var buildings = buildingQuery.ToComponentDataArray<Building>(Allocator.Temp);
            snapshot.buildings = new BuildingSnapshot[buildings.Length];
            var constructing = 0;
            for (var i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                var record = new BuildingSnapshot
                {
                    id = building.Id,
                    typeId = building.TypeId.ToString(),
                    widthClusters = building.WidthClusters,
                    depthRadialRings = building.DepthRadialRings,
                    anchorCluster = building.AnchorCluster,
                    anchorRadial = building.AnchorRadial,
                    built = 1
                };
                if (em.HasComponent<Workplace>(buildingEntities[i]))
                    record.paused = em.GetComponentData<Workplace>(buildingEntities[i]).Paused;
                if (em.HasComponent<Construction>(buildingEntities[i]))
                {
                    var construction = em.GetComponentData<Construction>(buildingEntities[i]);
                    record.built = 0;
                    record.constructionElapsed = construction.Elapsed;
                    record.constructionDuration = construction.Duration;
                    constructing++;
                }

                snapshot.buildings[i] = record;
            }

            buildingEntities.Dispose();
            buildings.Dispose();
            GameLog.Info(
                $"Captured snapshot: day {snapshot.day}, agents {snapshot.agents.Length}, buildings {snapshot.buildings.Length} ({constructing} constructing).");
            return snapshot;
        }

        public static void Apply(RunSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
                world.EntityManager.CompleteAllTrackedJobs();

            SimCommands.TryPost(SimClockCommand.Restore(snapshot.speed, playerPaused: true));

            SimCommands.TryRequestDespawnAllAgents();
            SimCommands.TryRequestDespawnAllBuildings();
            SimCommands.Playback();

            ApplyGameTime(snapshot);

            if (snapshot.buildings != null)
            {
                for (var i = 0; i < snapshot.buildings.Length; i++)
                {
                    var record = snapshot.buildings[i];
                    SimCommands.TryPost(new PlaceBuildingCommand
                    {
                        BuildingId = record.id,
                        TypeId = record.typeId,
                        WidthClusters = record.widthClusters,
                        DepthRadialRings = record.depthRadialRings,
                        AnchorCluster = record.anchorCluster,
                        AnchorRadial = record.anchorRadial,
                        ConstructionElapsed = record.constructionElapsed,
                        ConstructionDuration = record.constructionDuration,
                        InstantComplete = record.built != 0 ? (byte)1 : (byte)0
                    });
                }
            }

            if (snapshot.agents != null)
            {
                for (var i = 0; i < snapshot.agents.Length; i++)
                    EnqueueAgent(snapshot.agents[i]);
            }

            SimCommands.Playback();
            ApplyPausedBuildings(snapshot);
            ApplyResources(snapshot);
            GameLog.Info(
                $"Applied snapshot v{snapshot.version}: day {snapshot.day}, agents {snapshot.agents?.Length ?? 0}, buildings {snapshot.buildings?.Length ?? 0}.");
        }

        static void ApplyPausedBuildings(RunSnapshot snapshot)
        {
            if (snapshot.buildings == null)
                return;

            var any = false;
            for (var i = 0; i < snapshot.buildings.Length; i++)
            {
                var record = snapshot.buildings[i];
                if (record.paused == 0)
                    continue;
                SimCommands.TryPost(new SetWorkplacePausedCommand
                {
                    BuildingId = record.id,
                    Paused = 1
                });
                any = true;
            }

            if (any)
                SimCommands.Playback();
        }

        static void ApplyResources(RunSnapshot snapshot)
        {
            if (snapshot.resources == null || !SimWorld.TryGet(out var em, out var bag)
                || !em.HasBuffer<ResourceAmount>(bag))
                return;

            var stock = em.GetBuffer<ResourceAmount>(bag);

            for (var i = 0; i < snapshot.resources.Length; i++)
            {
                var row = snapshot.resources[i];
                if (string.IsNullOrWhiteSpace(row.resourceId))
                    continue;
                ResourceLedger.Set(stock, ContentId.EncodeOrEmpty(row.resourceId), row.amount);
            }
        }

        static void EnqueueAgent(AgentSnapshot record)
        {
            SimCommands.TryPost(new SpawnAgentCommand
            {
                Position = new float3(record.posX, record.posY, record.posZ),
                Facing = new float3(record.fwdX, record.fwdY, record.fwdZ),
                Target = new float3(record.targetX, record.targetY, record.targetZ),
                Speed = record.speed > 0.001f ? record.speed : 2f,
                AgentId = record.agentId,
                WorkplaceBuildingId = record.workplaceBuildingId,
                Arrived = record.arrived,
                Moving = record.moving,
                PlazaWalking = record.plazaWalking,
                PlazaTimer = record.plazaTimer,
                PlazaAngle = record.plazaAngle,
                PlazaRadius = record.plazaRadius,
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
                DayDuration = snapshot.dayDuration > 0f ? snapshot.dayDuration : 60f
            });
        }
    }
}
