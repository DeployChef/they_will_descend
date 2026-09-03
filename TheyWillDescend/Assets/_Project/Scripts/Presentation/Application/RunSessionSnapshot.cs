using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;

using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Gods;
using TheyWillDescend.Simulation.Research;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using TheyWillDescend.Shell;
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
            snapshot.playerPaused = control.TimePaused != 0;

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
            CaptureResolvedCatalog(em, bag, snapshot);

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

            using var buildingQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());

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
                    record.built = construction.IsDismantling ? (byte)1 : (byte)0;
                    record.constructionElapsed = construction.Elapsed;
                    record.constructionDuration = construction.Duration;
                    record.dismantling = construction.Dismantling;
                    constructing++;
                }

                snapshot.buildings[i] = record;
            }

            buildingEntities.Dispose();
            buildings.Dispose();
            CaptureGods(em, bag, snapshot);
            CaptureResearch(em, bag, snapshot);
            GameLog.Info(
                $"Captured snapshot: day {snapshot.day}, agents {snapshot.agents.Length}, buildings {snapshot.buildings.Length} ({constructing} constructing).");
            return snapshot;
        }

        public static bool BeginApply(RunSnapshot snapshot, TechCatalogAsset[] techCatalogs)
        {
            if (snapshot == null)
                return false;
            if (!SimWorld.TryGet(out var em, out var session)
                || !SimSessionAccess.HasLifecycleQueues(em, session))
            {
                GameLog.Error("Snapshot apply: required session lifecycle queues are missing.");
                return false;
            }
            em.CompleteAllTrackedJobs();
            if (!TryRestoreResolvedCatalog(em, session, snapshot))
            {
                GameLog.Error("Snapshot apply: resolved building catalog is missing or invalid.");
                return false;
            }

            RunPublisher.ClearLifecycleQueues(em, session);
            var lifecycle = em.GetComponentData<SimSession>(session);
            lifecycle.Phase = SimSessionPhase.Preparing;
            em.SetComponentData(session, lifecycle);
            var control = em.GetComponentData<SimControl>(session);
            control.Mode = SimRunMode.Off;
            control.SessionInGame = 0;
            control.TimePaused = 0;
            control.PlayerPaused = 0;
            control.BuildLocked = 0;
            control.DeltaTime = 0f;
            em.SetComponentData(session, control);

            em.GetBuffer<SimClockCommand>(session).Add(
                SimClockCommand.Restore(snapshot.speed, snapshot.playerPaused));
            em.GetBuffer<DespawnAllAgentsCommand>(session).Add(
                new DespawnAllAgentsCommand { Requested = 1 });
            em.GetBuffer<DespawnAllBuildingsCommand>(session).Add(
                new DespawnAllBuildingsCommand { Requested = 1 });
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
                        InstantComplete = record.built != 0 && record.dismantling == 0 ? (byte)1 : (byte)0,
                        Dismantling = record.dismantling,
                        Source = PlaceBuildingCommandSource.SnapshotRestore
                    });
                }
            }

            if (snapshot.agents != null)
            {
                for (var i = 0; i < snapshot.agents.Length; i++)
                    EnqueueAgent(snapshot.agents[i]);
            }

            ApplyPausedBuildings(snapshot);
            ApplyResources(snapshot);
            ApplyGods(snapshot);
            ResearchWorld.Populate(em, techCatalogs);
            ApplyResearch(snapshot);
            GameLog.Info(
                $"Snapshot setup queued v{snapshot.version}: day {snapshot.day}, agents {snapshot.agents?.Length ?? 0}, buildings {snapshot.buildings?.Length ?? 0}.");
            return true;
        }

        static void CaptureResolvedCatalog(
            EntityManager em,
            Entity session,
            RunSnapshot snapshot)
        {
            if (!em.HasBuffer<BuildingPrototype>(session)
                || !em.HasBuffer<BuildingCatalogCost>(session)
                || !em.HasBuffer<BuildingCatalogRecipe>(session))
                return;

            var prototypes = em.GetBuffer<BuildingPrototype>(session);
            snapshot.buildingCatalog = new ResolvedBuildingPrototypeSnapshot[prototypes.Length];
            for (var i = 0; i < prototypes.Length; i++)
            {
                var row = prototypes[i];
                snapshot.buildingCatalog[i] = new ResolvedBuildingPrototypeSnapshot
                {
                    typeId = row.TypeId.ToString(),
                    widthClusters = row.WidthClusters,
                    depthRadialRings = row.DepthRadialRings,
                    constructionDuration = row.ConstructionDuration,
                    constructionCrewSlots = row.ConstructionCrewSlots,
                    workplaceSlots = row.WorkplaceSlots,
                    researchWorkplace = row.ResearchWorkplace,
                    requiresUnlock = row.RequiresUnlock
                };
            }

            var costs = em.GetBuffer<BuildingCatalogCost>(session);
            snapshot.buildingCosts = new ResolvedBuildingCostSnapshot[costs.Length];
            for (var i = 0; i < costs.Length; i++)
            {
                var row = costs[i];
                snapshot.buildingCosts[i] = new ResolvedBuildingCostSnapshot
                {
                    typeId = row.TypeId.ToString(),
                    resourceId = row.ResourceId.ToString(),
                    amount = row.Amount
                };
            }

            var recipes = em.GetBuffer<BuildingCatalogRecipe>(session);
            snapshot.buildingRecipes = new ResolvedBuildingRecipeSnapshot[recipes.Length];
            for (var i = 0; i < recipes.Length; i++)
            {
                var row = recipes[i];
                snapshot.buildingRecipes[i] = new ResolvedBuildingRecipeSnapshot
                {
                    typeId = row.TypeId.ToString(),
                    kind = (byte)row.Kind,
                    resourceId = row.ResourceId.ToString(),
                    perHour = row.PerHour
                };
            }
        }

        static bool TryRestoreResolvedCatalog(
            EntityManager em,
            Entity session,
            RunSnapshot snapshot)
        {
            if (snapshot.buildingCatalog == null
                || snapshot.buildingCatalog.Length == 0
                || !em.HasBuffer<BuildingPrototype>(session)
                || !em.HasBuffer<BuildingCatalogCost>(session)
                || !em.HasBuffer<BuildingCatalogRecipe>(session))
                return false;

            for (var i = 0; i < snapshot.buildingCatalog.Length; i++)
            {
                var row = snapshot.buildingCatalog[i];
                if (row == null
                    || !ContentId.TryEncode(row.typeId, out _)
                    || row.widthClusters <= 0
                    || row.depthRadialRings <= 0)
                    return false;
            }

            var prototypes = em.GetBuffer<BuildingPrototype>(session);
            prototypes.Clear();
            for (var i = 0; i < snapshot.buildingCatalog.Length; i++)
            {
                var row = snapshot.buildingCatalog[i];
                prototypes.Add(new BuildingPrototype
                {
                    TypeId = ContentId.EncodeOrEmpty(row.typeId),
                    WidthClusters = row.widthClusters,
                    DepthRadialRings = row.depthRadialRings,
                    ConstructionDuration = math.max(0f, row.constructionDuration),
                    ConstructionCrewSlots = ConstructionCrew.ResolveSlots(row.constructionCrewSlots),
                    WorkplaceSlots = math.max(0, row.workplaceSlots),
                    ResearchWorkplace = row.researchWorkplace,
                    RequiresUnlock = row.requiresUnlock
                });
            }

            var costs = em.GetBuffer<BuildingCatalogCost>(session);
            costs.Clear();
            if (snapshot.buildingCosts != null)
            {
                for (var i = 0; i < snapshot.buildingCosts.Length; i++)
                {
                    var row = snapshot.buildingCosts[i];
                    if (row == null || row.amount <= 0.0001f)
                        continue;
                    var typeId = ContentId.EncodeOrEmpty(row.typeId);
                    var resourceId = ContentId.EncodeOrEmpty(row.resourceId);
                    if (typeId.IsEmpty || resourceId.IsEmpty)
                        continue;
                    costs.Add(new BuildingCatalogCost
                    {
                        TypeId = typeId,
                        ResourceId = resourceId,
                        Amount = row.amount
                    });
                }
            }

            var recipes = em.GetBuffer<BuildingCatalogRecipe>(session);
            recipes.Clear();
            if (snapshot.buildingRecipes != null)
            {
                for (var i = 0; i < snapshot.buildingRecipes.Length; i++)
                {
                    var row = snapshot.buildingRecipes[i];
                    if (row == null || row.perHour <= 0.0001f)
                        continue;
                    var typeId = ContentId.EncodeOrEmpty(row.typeId);
                    var resourceId = ContentId.EncodeOrEmpty(row.resourceId);
                    var kind = (BuildingRecipeKind)row.kind;
                    if (typeId.IsEmpty
                        || resourceId.IsEmpty
                        || (kind != BuildingRecipeKind.Input
                            && kind != BuildingRecipeKind.Output))
                        continue;
                    recipes.Add(new BuildingCatalogRecipe
                    {
                        TypeId = typeId,
                        Kind = kind,
                        ResourceId = resourceId,
                        PerHour = row.perHour
                    });
                }
            }

            return true;
        }

        static void ApplyPausedBuildings(RunSnapshot snapshot)

        {
            if (snapshot.buildings == null || !SimWorld.TryGet(out var em, out _))
                return;

            var pausedIds = new HashSet<int>();
            for (var i = 0; i < snapshot.buildings.Length; i++)
            {
                if (snapshot.buildings[i].paused != 0)
                    pausedIds.Add(snapshot.buildings[i].id);
            }

            if (pausedIds.Count == 0)
                return;

            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadWrite<Workplace>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            for (var b = 0; b < buildings.Length; b++)
            {
                if (!pausedIds.Contains(buildings[b].Id))
                    continue;
                var workplace = em.GetComponentData<Workplace>(entities[b]);
                workplace.Paused = 1;
                em.SetComponentData(entities[b], workplace);
            }
            buildings.Dispose();
        }


        static void ApplyResources(RunSnapshot snapshot)
        {
            if (snapshot.resources == null || !SimWorld.TryGet(out var em, out var bag)
                || !em.HasBuffer<ResourceAmount>(bag))
                return;

            var stock = em.GetBuffer<ResourceAmount>(bag);
            var info = em.HasBuffer<ResourceInfo>(bag) ? em.GetBuffer<ResourceInfo>(bag) : default;

            for (var i = 0; i < snapshot.resources.Length; i++)
            {
                var row = snapshot.resources[i];
                if (string.IsNullOrWhiteSpace(row.resourceId))
                    continue;
                var id = ContentId.EncodeOrEmpty(row.resourceId);
                if (info.IsCreated)
                    ResourceLedger.SetClamped(stock, info, id, row.amount);
                else
                    ResourceLedger.Set(stock, id, row.amount);
            }
        }

        static void CaptureGods(EntityManager em, Entity bag, RunSnapshot snapshot)
        {
            if (em.HasComponent<GodLoyalty>(bag))
            {
                var loyalty = em.GetComponentData<GodLoyalty>(bag);
                snapshot.faith = loyalty.Value;
                snapshot.faithMax = loyalty.EffectiveMax;
            }

            if (em.HasComponent<Timeline>(bag))
            {
                var timeline = em.GetComponentData<Timeline>(bag);
                snapshot.eraIndex = timeline.EraIndex;
                snapshot.eraStartDay = timeline.EraStartDay;
                snapshot.eraStartElapsed = timeline.EraStartElapsed;
                snapshot.previousMaxLoyalty = timeline.PreviousMaxLoyalty;
                snapshot.targetMaxLoyalty = timeline.TargetMaxLoyalty;
            }

            using var hq = em.CreateEntityQuery(
                ComponentType.ReadOnly<Headquarters>(),
                ComponentType.ReadOnly<PyramidFeedLine>());
            if (hq.IsEmptyIgnoreFilter)
                return;
            var hqEntity = hq.GetSingletonEntity();
            var feed = em.GetBuffer<PyramidFeedLine>(hqEntity);

            snapshot.pyramidFeed = new PyramidFeedSnapshot[feed.Length];
            for (var i = 0; i < feed.Length; i++)
            {
                snapshot.pyramidFeed[i] = new PyramidFeedSnapshot
                {
                    resourceId = feed[i].ResourceId.ToString(),
                    perHour = feed[i].PerHour
                };
            }
        }

        static void ApplyGods(RunSnapshot snapshot)
        {
            if (!SimWorld.TryGet(out var em, out var bag))
                return;

            if (em.HasComponent<GodLoyalty>(bag))
            {
                em.SetComponentData(bag, new GodLoyalty
                {
                    Value = snapshot.faith,
                    EffectiveMax = snapshot.faithMax > 0.0001f ? snapshot.faithMax : 100f
                });
            }

            if (em.HasComponent<Timeline>(bag))
            {
                em.SetComponentData(bag, new Timeline
                {
                    EraIndex = snapshot.eraIndex,
                    EraStartDay = snapshot.eraStartDay,
                    EraStartElapsed = snapshot.eraStartElapsed,
                    PreviousMaxLoyalty = snapshot.previousMaxLoyalty,
                    TargetMaxLoyalty = snapshot.targetMaxLoyalty > 0.0001f
                        ? snapshot.targetMaxLoyalty
                        : 100f
                });
            }

            if (snapshot.pyramidFeed == null)
                return;
            for (var i = 0; i < snapshot.pyramidFeed.Length; i++)
            {
                var row = snapshot.pyramidFeed[i];
                if (string.IsNullOrWhiteSpace(row.resourceId))
                    continue;
                SimCommands.TryPost(new SetPyramidFeedCommand
                {
                    ResourceId = ContentId.EncodeOrEmpty(row.resourceId),
                    PerHour = row.perHour
                });
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


        static void CaptureResearch(EntityManager em, Entity bag, RunSnapshot snapshot)
        {
            if (ResearchWorld.TryGetBoard(em, out var board)
                && em.HasComponent<ResearchControl>(board))
                snapshot.activeTechId = em.GetComponentData<ResearchControl>(board).ActiveTechId.ToString();

            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<TechInfo>(),
                ComponentType.ReadOnly<ResearchProgress>());
            using var infos = query.ToComponentDataArray<TechInfo>(Allocator.Temp);
            using var progress = query.ToComponentDataArray<ResearchProgress>(Allocator.Temp);
            snapshot.research = new ResearchLineSnapshot[infos.Length];
            for (var i = 0; i < infos.Length; i++)
            {
                snapshot.research[i] = new ResearchLineSnapshot
                {
                    techId = infos[i].TechId.ToString(),
                    accumulatedHours = progress[i].AccumulatedHours,
                    completed = progress[i].Completed,
                    costPaid = progress[i].CostPaid
                };
            }
        }

        static void ApplyResearch(RunSnapshot snapshot)
        {
            if (!SimWorld.TryGet(out var em, out _)
                || !ResearchWorld.TryGetBoard(em, out var board))
                return;

            using var query = em.CreateEntityQuery(
                ComponentType.ReadWrite<TechInfo>(),
                ComponentType.ReadWrite<ResearchProgress>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var infos = query.ToComponentDataArray<TechInfo>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var row = new ResearchProgress();
                if (snapshot.research != null)
                {
                    var techId = infos[i].TechId.ToString();
                    for (var s = 0; s < snapshot.research.Length; s++)
                    {
                        var saved = snapshot.research[s];
                        if (saved == null || saved.techId != techId)
                            continue;
                        row.AccumulatedHours = math.max(0f, saved.accumulatedHours);
                        row.Completed = saved.completed;
                        row.CostPaid = saved.costPaid;
                        break;
                    }
                }

                em.SetComponentData(entities[i], row);
            }

            var control = ResearchControl.Initial;
            control.ActiveTechId = ContentId.EncodeOrEmpty(snapshot.activeTechId);
            em.SetComponentData(board, control);
            ResearchRules.RebuildEffects(em);
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
            var current = em.GetComponentData<GameTime>(entity);
            em.SetComponentData(entity, new GameTime
            {
                Day = snapshot.day,
                ElapsedInDay = snapshot.elapsedInDay,
                DayDuration = snapshot.dayDuration > 0f ? snapshot.dayDuration : current.DayDuration,
                WorkShiftStartHour = current.WorkShiftStartHour,
                WorkShiftEndHour = current.WorkShiftEndHour
            });
        }
    }
}
