using System.Collections.Generic;
using TheyWillDescend.Content;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Gods;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Stories;
using TheyWillDescend.Simulation.Time;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace TheyWillDescend.Presentation.SimBridge
{
    /// <summary>
    /// Synchronous C# runtime bootstrap. Always creates a fresh session; the caller wipes first.
    /// </summary>
    public static class SimulationBootstrap
    {
        public static Entity InitializeRun(
            EntityManager em,
            BuildingCatalogAsset buildingCatalog,
            ResourceCatalogAsset resourceCatalog,
            SimRulesAsset rules,
            TimelineCatalogAsset timelineCatalog,
            StoryPackAsset storyPack,
            DifficultyProfile difficulty,
            out BuildingPrototype[] catalog,
            out BuildingCatalogRecipe[] recipes,
            out BuildingCatalogCost[] costs,
            out ResourceAmount[] stock,
            out ResourceInfo[] info,
            RadialGridConfig? gridConfig = null)
        {
            catalog = default;
            recipes = default;
            costs = default;
            stock = default;
            info = default;
            using var leftover = em.CreateEntityQuery(ComponentType.ReadOnly<SimSession>());
            if (!leftover.IsEmptyIgnoreFilter)
            {
                Debug.LogError("InitializeRun: a session still exists. Wipe the run first.");
                return Entity.Null;
            }

            var session = em.CreateEntity();
            RunWorld.Own(em, session);
            em.AddComponent<SimSession>(session);
#if UNITY_EDITOR
            em.SetName(session, "SimSession");
#endif

            em.AddComponentData(session, new SimControl
            {
                Mode = SimRunMode.Running,
                Speed = 1,
                DeltaTime = 0f
            });
            em.AddComponentData(session, new AgentIdSequence());
            em.AddComponentData(session, new WorkforceSummary());
            em.AddComponentData(session, new CityCore());
            em.AddBuffer<SimClockCommand>(session);
            em.AddBuffer<BuildingRejectedEvent>(session);

            var cfg = gridConfig ?? RadialGridConfig.Default;
            if (!cfg.IsValid)
                cfg = RadialGridConfig.Default;

            em.AddComponentData(session, new CityGrid
            {
                Config = cfg,
                Center = float3.zero,
                Ready = 1
            });
            em.AddComponentData(session, new BuildingIdSequence { NextBuildingId = 1 });
            em.AddBuffer<OccupiedCell>(session);
            em.AddComponentData(session, new RoadNetwork
            {
                SectionLength = RoadMath.SectionLength(cfg),
                NextSegmentId = 1
            });
            em.AddBuffer<RoadSegment>(session);

            if (rules != null)
            {
                em.AddComponentData(session, rules.CreateClock());
                em.AddComponentData(session, new PyramidConfig
                {
                    EraChangeHour = rules.EraChangeHour,
                    DefaultStockCap = rules.DefaultStockCap,
                    LoyaltyDecayPerDay = rules.LoyaltyDecayPerDay
                });
            }
            else
            {
                em.AddComponentData(session, new GameTime
                {
                    DayDuration = 60f,
                    WorkShiftStartHour = 6f,
                    WorkShiftEndHour = 18f
                });
                em.AddComponentData(session, new PyramidConfig
                {
                    EraChangeHour = 8f,
                    DefaultStockCap = 2000f,
                    LoyaltyDecayPerDay = 12f
                });
            }

            PopulateBuildingCatalog(em, session, buildingCatalog, difficulty, out catalog, out recipes, out costs);
            var defaultCap = rules != null ? rules.DefaultStockCap : 2000f;
            PopulateResourceCatalog(resourceCatalog, defaultCap, out stock, out info);
            PopulateTimelineCatalog(em, session, timelineCatalog);
            StoryBake.Apply(em, session, storyPack);
            return session;
        }

        public static void SpawnRunEntities(EntityManager em, Entity session, float workerSpeed, ResourceInfo[] info)
        {
            CreateAgentPrototype(em, session, workerSpeed);
            CreateHeadquarters(em, session, info);
        }

        static void CreateAgentPrototype(EntityManager em, Entity session, float speed)
        {
            var prototype = em.CreateEntity();
            RunWorld.Own(em, prototype);
            em.AddComponent<Prefab>(prototype);
            em.AddComponentData(prototype, new AgentLocomotion { Speed = speed });
            em.AddComponent<AgentAssignment>(prototype);
            em.AddComponent<AgentPlazaIdle>(prototype);
            em.AddComponent<AgentId>(prototype);
            em.AddComponentData(prototype, new AgentType { Kind = AgentKind.Worker });
            em.AddComponentData(session, new SimPrototypes { Agent = prototype });
#if UNITY_EDITOR
            em.SetName(prototype, "AgentPrototype");
#endif
        }

        static void PopulateBuildingCatalog(
            EntityManager em,
            Entity session,
            BuildingCatalogAsset catalog,
            DifficultyProfile difficulty,
            out BuildingPrototype[] catalogCopy,
            out BuildingCatalogRecipe[] recipeCopy,
            out BuildingCatalogCost[] costCopy)
        {
            catalogCopy = System.Array.Empty<BuildingPrototype>();
            recipeCopy = System.Array.Empty<BuildingCatalogRecipe>();
            costCopy = System.Array.Empty<BuildingCatalogCost>();
            var protoList = new List<BuildingPrototype>();
            var costList = new List<BuildingCatalogCost>();
            var recipeList = new List<BuildingCatalogRecipe>();

            if (catalog != null && catalog.Prefabs != null)
            {
                var seen = new HashSet<string>(System.StringComparer.Ordinal);
                var prefabs = catalog.Prefabs;
                for (var i = 0; i < prefabs.Count; i++)
                {
                    var prefab = prefabs[i];
                    if (prefab == null)
                        continue;

                    var stamp = prefab.GetComponent<BuildingStamp>();
                    if (stamp == null)
                        continue;

                    var typeId = stamp.TypeId;
                    if (string.IsNullOrEmpty(typeId) || !ContentId.TryEncode(typeId, out var typeKey))
                        continue;

                    if (!seen.Add(typeId))
                        continue;

                    protoList.Add(new BuildingPrototype
                    {
                        TypeId = typeKey,
                        WidthClusters = stamp.WidthClusters,
                        DepthRadialRings = stamp.DepthRadialRings,
                        ConstructionDuration = stamp.ConstructionDuration,
                        ConstructionCrewSlots = ConstructionCrew.ResolveSlots(stamp.ConstructionCrewSlots),
                        WorkplaceSlots = stamp.WorkplaceSlots,
                        ResearchWorkplace = stamp.IsResearchWorkplace ? (byte)1 : (byte)0,
                        RequiresUnlock = stamp.RequiresUnlock ? (byte)1 : (byte)0,
                        StrokePaint = stamp.StrokePaint ? (byte)1 : (byte)0
                    });

                    CollectCosts(costList, typeKey, stamp.Costs);
                    if (stamp.HasRecipe)
                    {
                        CollectRecipe(recipeList, typeKey, stamp.RecipeInputs, BuildingRecipeKind.Input);
                        CollectRecipe(recipeList, typeKey, stamp.RecipeOutputs, BuildingRecipeKind.Output);
                    }
                }
            }

            if (difficulty != null)
            {
                var rows = difficulty.Buildings;
                for (var i = 0; i < rows.Count; i++)
                    ApplyOverride(protoList, costList, recipeList, rows[i]);
            }

            catalogCopy = protoList.ToArray();
            recipeCopy = recipeList.ToArray();
            costCopy = costList.ToArray();
        }

        public static void WriteCatalog(
            EntityManager em,
            Entity session,
            BuildingPrototype[] catalog,
            BuildingCatalogRecipe[] recipes,
            BuildingCatalogCost[] costs,
            ResourceAmount[] stock,
            ResourceInfo[] info)
        {
            em.CompleteAllTrackedJobs();
            WriteBuffer(em.AddBuffer<BuildingPrototype>(session), catalog);
            WriteBuffer(em.AddBuffer<BuildingCatalogCost>(session), costs);
            WriteBuffer(em.AddBuffer<BuildingCatalogRecipe>(session), recipes);
            WriteBuffer(em.AddBuffer<ResourceAmount>(session), stock);
            WriteBuffer(em.AddBuffer<ResourceInfo>(session), info);
        }

        static void ApplyOverride(
            List<BuildingPrototype> prototypes,
            List<BuildingCatalogCost> costs,
            List<BuildingCatalogRecipe> recipes,
            in DifficultyBuildingOverride row)
        {
            var typeId = row.TypeId;
            if (string.IsNullOrEmpty(typeId) || !ContentId.TryEncode(typeId, out var typeKey))
                return;
            var index = -1;
            for (var p = 0; p < prototypes.Count; p++)
            {
                if (prototypes[p].TypeId != typeKey)
                    continue;
                index = p;
                break;
            }

            if (index < 0)
            {
                Debug.LogWarning($"Difficulty: unknown typeId {typeId}.");
                return;
            }

            var spec = prototypes[index];
            if (row.replaceConstruction)
                spec.ConstructionDuration = row.constructionDuration < 0f ? 0f : row.constructionDuration;
            if (row.replaceSlots)
                spec.WorkplaceSlots = row.workplaceSlots < 0 ? 0 : row.workplaceSlots;
            prototypes[index] = spec;

            if (row.replaceCosts)
            {
                costs.RemoveAll(c => c.TypeId == typeKey);
                CollectCosts(costs, typeKey, row.costs);
            }

            if (row.replaceRecipe)
            {
                recipes.RemoveAll(r => r.TypeId == typeKey);
                CollectRecipe(recipes, typeKey, row.recipeInputs, BuildingRecipeKind.Input);
                CollectRecipe(recipes, typeKey, row.recipeOutputs, BuildingRecipeKind.Output);
            }
        }

        static void WriteBuffer<T>(DynamicBuffer<T> buffer, T[] src)
            where T : unmanaged, IBufferElementData
        {
            var length = src != null ? src.Length : 0;
            buffer.Clear();
            buffer.ResizeUninitialized(length);
            for (var i = 0; i < length; i++)
                buffer[i] = src[i];
        }

        static void CollectCosts(
            List<BuildingCatalogCost> list,
            in FixedString64Bytes typeKey,
            BuildingCostEntry[] entries)
        {
            if (entries == null)
                return;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.Resource == null || entry.Amount <= 0.0001f)
                    continue;
                list.Add(new BuildingCatalogCost
                {
                    TypeId = typeKey,
                    ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                    Amount = entry.Amount
                });
            }
        }

        static void CollectRecipe(
            List<BuildingCatalogRecipe> list,
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
                list.Add(new BuildingCatalogRecipe
                {
                    TypeId = typeKey,
                    Kind = kind,
                    ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                    PerHour = entry.PerHour
                });
            }
        }

        static void PopulateResourceCatalog(
            ResourceCatalogAsset catalog,
            float defaultCap,
            out ResourceAmount[] stock,
            out ResourceInfo[] info)
        {
            stock = System.Array.Empty<ResourceAmount>();
            info = System.Array.Empty<ResourceInfo>();
            if (catalog == null || catalog.Resources == null)
                return;

            var amounts = new List<ResourceAmount>();
            var infos = new List<ResourceInfo>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            var resources = catalog.Resources;
            for (var i = 0; i < resources.Count; i++)
            {
                var definition = resources[i];
                if (definition == null)
                    continue;

                var id = definition.ResourceId;
                if (string.IsNullOrEmpty(id) || !ContentId.TryEncode(id, out var key))
                    continue;

                if (!seen.Add(id))
                    continue;

                amounts.Add(new ResourceAmount { ResourceId = key, Amount = 0f });
                var cap = definition.StockCap > 0.0001f ? definition.StockCap : defaultCap;
                infos.Add(new ResourceInfo
                {
                    ResourceId = key,
                    DisplayName = definition.DisplayName,
                    EnergyValue = definition.EnergyValue,
                    StockCap = cap,
                    CanFeed = definition.CanFeed ? (byte)1 : (byte)0,
                    Category = (byte)definition.Category
                });
            }

            stock = amounts.ToArray();
            info = infos.ToArray();
        }

        static void PopulateTimelineCatalog(EntityManager em, Entity session, TimelineCatalogAsset catalog)
        {
            var eraList = new List<EraLine>();
            var tributeList = new List<EraTributeLine>();
            var firstMax = 100f;
            if (catalog != null && catalog.Eras != null)
            {
                var specs = catalog.Eras;
                for (var i = 0; i < specs.Length; i++)
                {
                    var spec = specs[i];
                    if (!ContentId.TryEncode(ContentId.Normalize(spec.eraId, spec.displayName), out var id))
                        continue;

                    var days = spec.durationDays > 0 ? spec.durationDays : 1;
                    var max = spec.maxLoyalty;
                    if (max < 0f) max = 0f;
                    else if (max > 100f) max = 100f;
                    if (eraList.Count == 0)
                        firstMax = max;

                    var name = string.IsNullOrWhiteSpace(spec.displayName) ? spec.eraId : spec.displayName;
                    var summary = spec.summary ?? string.Empty;
                    if (summary.Length > 500)
                        summary = summary.Substring(0, 500);

                    eraList.Add(new EraLine
                    {
                        EraId = id,
                        DisplayName = name,
                        DurationDays = days,
                        MaxLoyalty = max,
                        TributeEnergyMul = spec.tributeEnergyMultiplier > 0.0001f ? spec.tributeEnergyMultiplier : 1f,
                        LoyaltyPerEnergy = spec.loyaltyPerEnergy < 0f ? 0f : spec.loyaltyPerEnergy,
                        Summary = summary
                    });

                    var eraIndex = eraList.Count - 1;
                    var gifts = spec.tribute;
                    if (gifts == null)
                        continue;
                    for (var t = 0; t < gifts.Length; t++)
                    {
                        var res = gifts[t];
                        if (res == null || !ContentId.TryEncode(res.ResourceId, out var resId))
                            continue;
                        tributeList.Add(new EraTributeLine { EraIndex = eraIndex, ResourceId = resId });
                    }
                }
            }

            WriteBuffer(em.AddBuffer<EraLine>(session), eraList.ToArray());
            WriteBuffer(em.AddBuffer<EraTributeLine>(session), tributeList.ToArray());
            em.AddComponentData(session, new Timeline
            {
                EraIndex = 0,
                PreviousMaxLoyalty = firstMax,
                TargetMaxLoyalty = firstMax
            });
            var loyalty = GodLoyalty.Full();
            loyalty.Value = firstMax;
            loyalty.EffectiveMax = firstMax;
            em.AddComponentData(session, loyalty);
        }

        static void CreateHeadquarters(EntityManager em, Entity session, ResourceInfo[] info)
        {
            var hqEntity = em.CreateEntity();
            RunWorld.Own(em, hqEntity);
            em.AddComponent<Headquarters>(hqEntity);
            if (em.HasComponent<CityCore>(session))
                em.SetComponentData(session, new CityCore { Headquarters = hqEntity });
            else
                em.AddComponentData(session, new CityCore { Headquarters = hqEntity });
            em.AddComponentData(hqEntity, LocalTransform.FromPosition(float3.zero));
#if UNITY_EDITOR
            em.SetName(hqEntity, "Headquarters");
#endif
            var feedLines = new List<PyramidFeedLine>();
            if (info != null)
            {
                for (var i = 0; i < info.Length; i++)
                {
                    if (info[i].CanFeed == 0)
                        continue;
                    feedLines.Add(new PyramidFeedLine { ResourceId = info[i].ResourceId, PerHour = 0f });
                }
            }

            WriteBuffer(em.AddBuffer<PyramidFeedLine>(hqEntity), feedLines.ToArray());
        }
    }
}
