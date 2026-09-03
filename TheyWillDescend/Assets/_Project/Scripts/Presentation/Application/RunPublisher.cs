using TheyWillDescend.Content;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Research;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Session-start publisher: overlay difficulty onto the baked catalog, then
    /// write the chosen scenario. Systems never call this. A/B later is another
    /// overlay in the same place, not a service in the tick.
    /// </summary>
    public static class RunPublisher
    {
        public static bool BeginRun(
            ScenarioDefinition scenario,
            DifficultyProfile difficulty,
            TechCatalogAsset[] techCatalogs)
        {
            var requestedDifficulty = difficulty;
            if (scenario != null
                && !scenario.TryResolveDifficulty(requestedDifficulty, out difficulty))
            {
                GameLog.Error(
                    $"RunPublisher: difficulty {requestedDifficulty.name} is not allowed by scenario {scenario.name}.");
                return false;
            }
            if (!SimWorld.TryGet(out var em, out var session))
            {
                GameLog.Error("RunPublisher: SimSession missing.");
                return false;
            }
            if (!SimSessionAccess.HasLifecycleQueues(em, session))
            {
                GameLog.Error("RunPublisher: required lifecycle queues are missing from the session bake.");
                return false;
            }

            em.CompleteAllTrackedJobs();
            if (!RebuildResolvedCatalog(em, session))
            {
                GameLog.Error("RunPublisher: base/resolved building catalogs are missing.");
                return false;
            }
            ClearLifecycleQueues(em, session);
            var lifecycle = em.GetComponentData<SimSession>(session);
            lifecycle.Phase = SimSessionPhase.Preparing;
            em.SetComponentData(session, lifecycle);
            SetSimulationOff(em, session);

            em.GetBuffer<DespawnAllAgentsCommand>(session).Add(
                new DespawnAllAgentsCommand { Requested = 1 });
            em.GetBuffer<DespawnAllBuildingsCommand>(session).Add(
                new DespawnAllBuildingsCommand { Requested = 1 });
            ApplyDifficulty(em, session, difficulty);
            ApplyScenario(em, session, scenario);
            var techs = ResearchWorld.Populate(em, techCatalogs);

            var name = scenario != null ? scenario.name : "none";
            var diff = difficulty != null ? difficulty.name : "stamp defaults";
            var houses = em.GetBuffer<PendingScenarioPlace>(session).Length;
            GameLog.Info(
                $"Run setup queued: scenario={name}, difficulty={diff}, houses={houses}, techs={techs}.");
            return true;
        }

        /// <summary>
        /// Runtime houses/agents live in the default world, not the SubScene.
        /// Unloading Game does not destroy them — a new run must wipe first.
        /// </summary>
        public static bool BeginReset()
        {
            if (!SimWorld.TryGet(out var em, out var session))
                return false;
            if (!SimSessionAccess.HasLifecycleQueues(em, session))
            {
                GameLog.Error("RunPublisher reset: required lifecycle queues are missing.");
                return false;
            }

            em.CompleteAllTrackedJobs();
            ResearchWorld.DestroyAll(em);
            ClearLifecycleQueues(em, session);
            var lifecycle = em.GetComponentData<SimSession>(session);
            lifecycle.Phase = SimSessionPhase.Resetting;
            em.SetComponentData(session, lifecycle);
            SetSimulationOff(em, session);
            em.GetBuffer<DespawnAllAgentsCommand>(session).Add(
                new DespawnAllAgentsCommand { Requested = 1 });
            em.GetBuffer<DespawnAllBuildingsCommand>(session).Add(
                new DespawnAllBuildingsCommand { Requested = 1 });
            return true;
        }

        static void SetSimulationOff(EntityManager em, Entity session)
        {
            var control = em.GetComponentData<SimControl>(session);
            control.Mode = SimRunMode.Off;
            control.SessionInGame = 0;
            control.TimePaused = 0;
            control.PlayerPaused = 0;
            control.BuildLocked = 0;
            control.DeltaTime = 0f;
            em.SetComponentData(session, control);
        }

        internal static void ClearLifecycleQueues(EntityManager em, Entity session)
        {
            em.SetComponentData(session, new PendingScenarioSpawns());
            em.GetBuffer<PendingScenarioPlace>(session).Clear();
            em.GetBuffer<SimClockCommand>(session).Clear();
            em.GetBuffer<DespawnAllAgentsCommand>(session).Clear();
            em.GetBuffer<DespawnAllBuildingsCommand>(session).Clear();
            em.GetBuffer<SpawnAgentCommand>(session).Clear();
            em.GetBuffer<TheyWillDescend.Simulation.Gods.SetPyramidFeedCommand>(session).Clear();
        }



        internal static bool RebuildResolvedCatalog(EntityManager em, Entity session)
        {
            if (!em.HasBuffer<BaseBuildingPrototype>(session)
                || !em.HasBuffer<BaseBuildingCatalogCost>(session)
                || !em.HasBuffer<BaseBuildingCatalogRecipe>(session)
                || !em.HasBuffer<BuildingPrototype>(session)
                || !em.HasBuffer<BuildingCatalogCost>(session)
                || !em.HasBuffer<BuildingCatalogRecipe>(session))
                return false;

            var basePrototypes = em.GetBuffer<BaseBuildingPrototype>(session);
            var prototypes = em.GetBuffer<BuildingPrototype>(session);
            prototypes.Clear();
            for (var i = 0; i < basePrototypes.Length; i++)
                prototypes.Add(basePrototypes[i].ToResolved());

            var baseCosts = em.GetBuffer<BaseBuildingCatalogCost>(session);
            var costs = em.GetBuffer<BuildingCatalogCost>(session);
            costs.Clear();
            for (var i = 0; i < baseCosts.Length; i++)
                costs.Add(baseCosts[i].ToResolved());

            var baseRecipes = em.GetBuffer<BaseBuildingCatalogRecipe>(session);
            var recipes = em.GetBuffer<BuildingCatalogRecipe>(session);
            recipes.Clear();
            for (var i = 0; i < baseRecipes.Length; i++)
                recipes.Add(baseRecipes[i].ToResolved());

            return prototypes.Length > 0;
        }

        static void ApplyDifficulty(EntityManager em, Entity session, DifficultyProfile difficulty)
        {
            if (difficulty == null || !em.HasBuffer<BuildingPrototype>(session))
                return;

            var rows = difficulty.Buildings;
            for (var i = 0; i < rows.Count; i++)
                ApplyOverride(em, session, rows[i]);
        }

        static void ApplyOverride(EntityManager em, Entity session, in DifficultyBuildingOverride row)
        {
            var typeId = row.TypeId;
            if (string.IsNullOrEmpty(typeId) || !ContentId.TryEncode(typeId, out var typeKey))
                return;
            if (!em.HasBuffer<BuildingPrototype>(session))
                return;

            var prototypes = em.GetBuffer<BuildingPrototype>(session);
            var index = -1;
            for (var p = 0; p < prototypes.Length; p++)
            {
                if (prototypes[p].TypeId != typeKey)
                    continue;
                index = p;
                break;
            }

            if (index < 0)
            {
                GameLog.Warning($"Difficulty: unknown typeId {typeId}.");
                return;
            }

            var spec = prototypes[index];
            if (row.replaceConstruction)
                spec.ConstructionDuration = row.constructionDuration < 0f ? 0f : row.constructionDuration;
            if (row.replaceSlots)
                spec.WorkplaceSlots = row.workplaceSlots < 0 ? 0 : row.workplaceSlots;
            prototypes[index] = spec;

            if (row.replaceCosts && em.HasBuffer<BuildingCatalogCost>(session))
            {
                ReplaceCosts(em.GetBuffer<BuildingCatalogCost>(session), typeKey, row.costs);
            }

            if (row.replaceRecipe && em.HasBuffer<BuildingCatalogRecipe>(session))
            {
                ReplaceRecipe(em.GetBuffer<BuildingCatalogRecipe>(session), typeKey, row.recipeInputs, row.recipeOutputs);
            }
        }

        static void ReplaceCosts(
            DynamicBuffer<BuildingCatalogCost> buffer,
            in FixedString64Bytes typeKey,
            BuildingCostEntry[] entries)
        {
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].TypeId == typeKey)
                    buffer.RemoveAt(i);
            }

            if (entries == null)
                return;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.Resource == null || entry.Amount <= 0.0001f)
                    continue;
                buffer.Add(new BuildingCatalogCost
                {
                    TypeId = typeKey,
                    ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                    Amount = entry.Amount
                });
            }
        }

        static void ReplaceRecipe(
            DynamicBuffer<BuildingCatalogRecipe> buffer,
            in FixedString64Bytes typeKey,
            ResourceRate[] inputs,
            ResourceRate[] outputs)
        {
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].TypeId == typeKey)
                    buffer.RemoveAt(i);
            }

            AddRecipe(buffer, typeKey, inputs, BuildingRecipeKind.Input);
            AddRecipe(buffer, typeKey, outputs, BuildingRecipeKind.Output);
        }

        static void AddRecipe(
            DynamicBuffer<BuildingCatalogRecipe> buffer,
            in FixedString64Bytes typeKey,
            ResourceRate[] rates,
            BuildingRecipeKind kind)
        {
            if (rates == null)
                return;
            for (var i = 0; i < rates.Length; i++)
            {
                var entry = rates[i];
                if (entry.Resource == null || entry.PerHour <= 0.0001f)
                    continue;
                buffer.Add(new BuildingCatalogRecipe
                {
                    TypeId = typeKey,
                    Kind = kind,
                    ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                    PerHour = entry.PerHour
                });
            }
        }

        static void ApplyScenario(EntityManager em, Entity session, ScenarioDefinition scenario)
        {
            WritePendingPlaces(em, session, scenario);
            WriteStock(em, session, scenario);
            WriteWorkers(em, session, scenario);
        }

        static void WritePendingPlaces(EntityManager em, Entity session, ScenarioDefinition scenario)
        {
            var pending = em.GetBuffer<PendingScenarioPlace>(session);
            pending.Clear();
            if (scenario == null)
                return;

            var buildings = scenario.Buildings;
            for (var i = 0; i < buildings.Count; i++)
            {
                var record = buildings[i];
                var typeId = ContentId.EncodeOrEmpty(record.TypeId);
                if (typeId.IsEmpty)
                    continue;
                pending.Add(new PendingScenarioPlace
                {
                    TypeId = typeId,
                    Cluster = record.Cluster,
                    Radial = record.Radial
                });
            }
        }

        static void WriteStock(EntityManager em, Entity session, ScenarioDefinition scenario)
        {
            if (!em.HasBuffer<ResourceAmount>(session) || !em.HasBuffer<ResourceInfo>(session))
                return;

            var stock = em.GetBuffer<ResourceAmount>(session);
            var info = em.GetBuffer<ResourceInfo>(session);
            for (var i = 0; i < stock.Length; i++)
                ResourceLedger.Set(stock, stock[i].ResourceId, 0f);

            if (scenario == null)
                return;
            var rows = scenario.StartingStock;
            if (rows == null)
                return;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Resource == null)
                    continue;
                ResourceLedger.SetClamped(
                    stock,
                    info,
                    ContentId.EncodeOrEmpty(row.Resource.ResourceId),
                    row.Amount);
            }
        }

        static void WriteWorkers(EntityManager em, Entity session, ScenarioDefinition scenario)
        {
            var workers = scenario != null ? scenario.StartingWorkers : 0;
            em.SetComponentData(session, new PendingScenarioSpawns { Workers = workers });
        }
    }
}
