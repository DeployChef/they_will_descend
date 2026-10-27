using TheyWillDescend.Presentation.Infrastructure;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Gods;
using TheyWillDescend.Simulation.Research;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Presentation.SimBridge
{
    /// <summary>
    /// Writes a slot DTO onto a freshly bootstrapped session. No command queues.
    /// </summary>
    public static class SnapshotRestore
    {
        public static bool Apply(
            EntityManager em,
            RunSnapshot snapshot,
            TechCatalogAsset[] techCatalogs,
            float workerSpeed)
        {
            if (snapshot == null)
                return false;
            if (!SimSessionAccess.TryGet(em, out var session))
            {
                GameLog.Error("Snapshot apply: SimSession missing.");
                return false;
            }

            try
            {
                if (!TryRestoreResolvedCatalog(em, session, snapshot))
                {
                    GameLog.Error("Snapshot apply: resolved building catalog is missing or invalid.");
                    return false;
                }
            }
            catch (System.Exception e)
            {
                GameLog.Error($"Snapshot apply: catalog buffers ({e.Message}). Houses still restore from the snapshot copy.");
            }

            ApplyGameTime(em, session, snapshot);
            ApplyClock(em, session, snapshot);
            PlaceBuildings(em, session, snapshot);
            SimulationBootstrap.SpawnRunEntities(em, session, workerSpeed, System.Array.Empty<ResourceInfo>());
            SeedInnerRing(em, session);
            SpawnAgents(em, session, snapshot);
            ApplyResources(em, session, snapshot);
            ApplyGods(em, session, snapshot);
            ResearchWorld.Populate(em, techCatalogs);
            ApplyResearch(em, snapshot);
            StorySave.QueueRestore(em, session, snapshot);
            GameLog.Info(
                $"Snapshot restored v{snapshot.version}: day {snapshot.day}, agents {snapshot.agents?.Length ?? 0}, buildings {snapshot.buildings?.Length ?? 0}.");
            return true;
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
                    if (typeId.IsEmpty || resourceId.IsEmpty)
                        continue;
                    var kind = (BuildingRecipeKind)row.kind;
                    if (kind != BuildingRecipeKind.Input && kind != BuildingRecipeKind.Output)
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

        static void ApplyGameTime(EntityManager em, Entity session, RunSnapshot snapshot)
        {
            if (!em.HasComponent<GameTime>(session))
                return;

            var current = em.GetComponentData<GameTime>(session);
            em.SetComponentData(session, new GameTime
            {
                Day = snapshot.day,
                ElapsedInDay = snapshot.elapsedInDay,
                DayDuration = snapshot.dayDuration > 0f ? snapshot.dayDuration : current.DayDuration,
                WorkShiftStartHour = current.WorkShiftStartHour,
                WorkShiftEndHour = current.WorkShiftEndHour
            });
        }

        static void ApplyClock(EntityManager em, Entity session, RunSnapshot snapshot)
        {
            if (!em.HasComponent<SimControl>(session))
                return;

            var control = em.GetComponentData<SimControl>(session);
            control.Speed = snapshot.speed < 1 ? 1 : snapshot.speed > 3 ? 3 : snapshot.speed;
            control.TimePaused = snapshot.playerPaused ? (byte)1 : (byte)0;
            control.PlayerPaused = 0;
            control.Mode = SimRunMode.Off;
            control.SessionInGame = 0;
            em.SetComponentData(session, control);
        }

        static void PlaceBuildings(EntityManager em, Entity session, RunSnapshot snapshot)
        {
            if (snapshot.buildings == null || !em.HasComponent<CityGrid>(session))
                return;

            var grid = em.GetComponentData<CityGrid>(session);
            CatalogFromSnapshot(snapshot, out var catalog, out var recipes);
            for (var i = 0; i < snapshot.buildings.Length; i++)
            {
                var record = snapshot.buildings[i];
                if (record == null || record.widthClusters <= 0 || record.depthRadialRings <= 0)
                    continue;

                var spec = new BuildingPlaceSpec
                {
                    BuildingId = record.id,
                    TypeId = record.typeId,
                    AnchorCluster = record.anchorCluster,
                    AnchorRadial = record.anchorRadial,
                    ConstructionElapsed = record.constructionElapsed,
                    ConstructionDuration = record.constructionDuration,
                    InstantComplete = record.built != 0 && record.dismantling == 0 ? (byte)1 : (byte)0,
                    Dismantling = record.dismantling,
                    DesiredWorkers = record.desiredWorkers,
                    Paused = record.paused,
                    Mode = BuildingPlaceMode.Restore
                };
                BuildingPlacement.TryPlace(em, session, ref grid, in spec, catalog, recipes);
            }

            em.SetComponentData(session, grid);
        }

        static void CatalogFromSnapshot(
            RunSnapshot snapshot,
            out BuildingPrototype[] catalog,
            out BuildingCatalogRecipe[] recipes)
        {
            if (snapshot.buildingCatalog == null || snapshot.buildingCatalog.Length == 0)
            {
                catalog = System.Array.Empty<BuildingPrototype>();
                recipes = System.Array.Empty<BuildingCatalogRecipe>();
                return;
            }

            catalog = new BuildingPrototype[snapshot.buildingCatalog.Length];
            for (var i = 0; i < snapshot.buildingCatalog.Length; i++)
            {
                var row = snapshot.buildingCatalog[i];
                catalog[i] = new BuildingPrototype
                {
                    TypeId = ContentId.EncodeOrEmpty(row != null ? row.typeId : null),
                    WidthClusters = row != null ? row.widthClusters : 0,
                    DepthRadialRings = row != null ? row.depthRadialRings : 0,
                    ConstructionDuration = row != null ? math.max(0f, row.constructionDuration) : 0f,
                    ConstructionCrewSlots = ConstructionCrew.ResolveSlots(row != null ? row.constructionCrewSlots : 0),
                    WorkplaceSlots = row != null ? math.max(0, row.workplaceSlots) : 0,
                    ResearchWorkplace = row != null ? row.researchWorkplace : (byte)0,
                    RequiresUnlock = row != null ? row.requiresUnlock : (byte)0
                };
            }

            if (snapshot.buildingRecipes == null || snapshot.buildingRecipes.Length == 0)
            {
                recipes = System.Array.Empty<BuildingCatalogRecipe>();
                return;
            }

            var list = new System.Collections.Generic.List<BuildingCatalogRecipe>(snapshot.buildingRecipes.Length);
            for (var i = 0; i < snapshot.buildingRecipes.Length; i++)
            {
                var row = snapshot.buildingRecipes[i];
                if (row == null || row.perHour <= 0.0001f)
                    continue;
                var typeId = ContentId.EncodeOrEmpty(row.typeId);
                var resourceId = ContentId.EncodeOrEmpty(row.resourceId);
                if (typeId.IsEmpty || resourceId.IsEmpty)
                    continue;
                var kind = (BuildingRecipeKind)row.kind;
                if (kind != BuildingRecipeKind.Input && kind != BuildingRecipeKind.Output)
                    continue;
                list.Add(new BuildingCatalogRecipe
                {
                    TypeId = typeId,
                    Kind = kind,
                    ResourceId = resourceId,
                    PerHour = row.perHour
                });
            }

            recipes = list.ToArray();
        }

        static void SeedInnerRing(EntityManager em, Entity session)
        {
            if (!em.HasComponent<CityGrid>(session) || !em.HasComponent<RoadNetwork>(session))
                return;
            var grid = em.GetComponentData<CityGrid>(session);
            var network = em.GetComponentData<RoadNetwork>(session);
            RoadSections.SeedInnerRing(em, session, ref grid, ref network);
            em.SetComponentData(session, grid);
            em.SetComponentData(session, network);
        }

        static void SpawnAgents(EntityManager em, Entity session, RunSnapshot snapshot)
        {
            if (snapshot.agents == null)
                return;

            for (var i = 0; i < snapshot.agents.Length; i++)
            {
                var record = snapshot.agents[i];
                if (record == null)
                    continue;
                AgentSpawn.Spawn(em, session, new SpawnAgentRequest
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
                    PlazaDirection = record.plazaDirection,
                    HasPose = 1,
                    Kind = (AgentKind)record.agentType
                });
            }
        }

        static void ApplyResources(EntityManager em, Entity session, RunSnapshot snapshot)
        {
            if (snapshot.resources == null || !em.HasBuffer<ResourceAmount>(session))
                return;

            var stock = em.GetBuffer<ResourceAmount>(session);
            var info = em.HasBuffer<ResourceInfo>(session) ? em.GetBuffer<ResourceInfo>(session) : default;

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

        static void ApplyGods(EntityManager em, Entity session, RunSnapshot snapshot)
        {
            if (em.HasComponent<GodLoyalty>(session))
            {
                em.SetComponentData(session, new GodLoyalty
                {
                    Value = snapshot.faith,
                    EffectiveMax = snapshot.faithMax > 0.0001f ? snapshot.faithMax : 100f
                });
            }

            if (em.HasComponent<Timeline>(session))
            {
                em.SetComponentData(session, new Timeline
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
            using var hqQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<Headquarters>(),
                ComponentType.ReadWrite<PyramidFeedLine>());
            if (hqQuery.IsEmptyIgnoreFilter)
                return;

            var hq = hqQuery.GetSingletonEntity();
            var feed = em.GetBuffer<PyramidFeedLine>(hq);
            var info = em.HasBuffer<ResourceInfo>(session)
                ? em.GetBuffer<ResourceInfo>(session)
                : default;
            if (!info.IsCreated)
                return;

            for (var i = 0; i < snapshot.pyramidFeed.Length; i++)
            {
                var row = snapshot.pyramidFeed[i];
                if (string.IsNullOrWhiteSpace(row.resourceId))
                    continue;
                PyramidFeed.SetPerHour(
                    feed,
                    info,
                    ContentId.EncodeOrEmpty(row.resourceId),
                    row.perHour);
            }
        }

        static void ApplyResearch(EntityManager em, RunSnapshot snapshot)
        {
            if (!ResearchWorld.TryGetBoard(em, out var board))
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
    }
}
