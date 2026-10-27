using TheyWillDescend.Presentation.Infrastructure;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Gods;
using TheyWillDescend.Simulation.Research;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Presentation.SimBridge
{
    /// <summary>
    /// Maps ECS write model → slot DTO. Restore is <see cref="SnapshotRestore"/>.
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

            if (em.HasComponent<GameTime>(bag))
            {
                var time = em.GetComponentData<GameTime>(bag);
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
                    plazaRadius = plazas[i].Radius,
                    plazaDirection = plazas[i].Direction
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
                {
                    var wp = em.GetComponentData<Workplace>(buildingEntities[i]);
                    record.paused = wp.Paused;
                    record.desiredWorkers = wp.DesiredWorkers;
                }

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
            StorySave.Capture(em, bag, snapshot);
            GameLog.Info(
                $"Captured snapshot: day {snapshot.day}, agents {snapshot.agents.Length}, buildings {snapshot.buildings.Length} ({constructing} constructing).");
            return snapshot;
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
    }
}
