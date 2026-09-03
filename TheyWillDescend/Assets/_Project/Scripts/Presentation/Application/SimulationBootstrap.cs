using System.Collections.Generic;
using TheyWillDescend.Content;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Gods;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Synchronous pure C# runtime bootstrap.
    /// Replaces the SubScene bake cycle for catalogs, grid, rules, and the Headquarters.
    /// Runs in 0 milliseconds and eliminates the 30-second bake timeout.
    /// </summary>
    public static class SimulationBootstrap
    {
        public static Entity InitializeRun(
            EntityManager em,
            BuildingCatalogAsset buildingCatalog,
            ResourceCatalogAsset resourceCatalog,
            SimRulesAsset rules,
            TimelineCatalogAsset timelineCatalog,
            RadialGridConfig? gridConfig = null)
        {

            using var sessionQuery = em.CreateEntityQuery(ComponentType.ReadOnly<SimSession>());

            Entity session;
            if (sessionQuery.CalculateEntityCount() > 0)
            {
                using var sessions = sessionQuery.ToEntityArray(Allocator.Temp);
                session = sessions[0];
                for (var i = 1; i < sessions.Length; i++)
                    em.DestroyEntity(sessions[i]);
            }
            else
            {
                session = em.CreateEntity();
            }

            using var gameTimeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<GameTime>());
            if (gameTimeQuery.CalculateEntityCount() > 0)
            {
                using var gameTimes = gameTimeQuery.ToEntityArray(Allocator.Temp);
                for (var i = 0; i < gameTimes.Length; i++)
                {
                    if (gameTimes[i] != session)
                        em.DestroyEntity(gameTimes[i]);
                }
            }


            // Lifecycle & Control
            EnsureComponent(em, session, new SimControl
            {
                Mode = SimRunMode.Off,
                Speed = 1,
                DeltaTime = 0f
            });
            EnsureComponent(em, session, new SimSession { Phase = SimSessionPhase.Unprepared });
            EnsureComponent(em, session, new AgentIdSequence());
            EnsureComponent(em, session, new PendingScenarioSpawns());

            // Lifecycle command queues
            EnsureBuffer<SimClockCommand>(em, session);
            EnsureBuffer<DespawnAllAgentsCommand>(em, session);
            EnsureBuffer<DespawnAllBuildingsCommand>(em, session);
            EnsureBuffer<BuildingRejectedEvent>(em, session);
            EnsureBuffer<PendingScenarioPlace>(em, session);



            // City Grid
            var cfg = gridConfig ?? RadialGridConfig.Default;
            if (!cfg.IsValid)
                cfg = RadialGridConfig.Default;

            EnsureComponent(em, session, new CityGrid
            {
                Config = cfg,
                Center = float3.zero,
                Ready = 1,
                NextBuildingId = 1
            });
            EnsureBuffer<OccupiedCell>(em, session);

            // Rules, Clock & PyramidConfig
            if (rules != null)
            {
                EnsureComponent(em, session, rules.CreateClock());
                EnsureComponent(em, session, new PyramidConfig
                {
                    EraChangeHour = rules.EraChangeHour,
                    DefaultStockCap = rules.DefaultStockCap,
                    LoyaltyDecayPerDay = rules.LoyaltyDecayPerDay
                });
            }
            else
            {
                EnsureComponent(em, session, new GameTime
                {
                    DayDuration = 60f,
                    WorkShiftStartHour = 6f,
                    WorkShiftEndHour = 18f
                });
                EnsureComponent(em, session, new PyramidConfig
                {
                    EraChangeHour = 8f,
                    DefaultStockCap = 2000f,
                    LoyaltyDecayPerDay = 12f
                });
            }

            // SimPrototypes
            var workerSpeed = rules != null ? rules.WorkerSpeed : 2f;
            EnsureAgentPrototype(em, session, workerSpeed);

            // Catalogs
            PopulateBuildingCatalog(em, session, buildingCatalog);
            var defaultCap = rules != null ? rules.DefaultStockCap : 2000f;
            PopulateResourceCatalog(em, session, resourceCatalog, defaultCap);
            PopulateTimelineCatalog(em, session, timelineCatalog);

            // Headquarters
            EnsureHeadquarters(em, session);

            return session;
        }

        static void EnsureComponent<T>(EntityManager em, Entity entity, in T data) where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(entity))
                em.SetComponentData(entity, data);
            else
                em.AddComponentData(entity, data);
        }

        static DynamicBuffer<T> EnsureBuffer<T>(EntityManager em, Entity entity) where T : unmanaged, IBufferElementData
        {
            return em.HasBuffer<T>(entity) ? em.GetBuffer<T>(entity) : em.AddBuffer<T>(entity);
        }

        static void EnsureAgentPrototype(EntityManager em, Entity session, float speed)
        {
            var existing = em.HasComponent<SimPrototypes>(session)
                ? em.GetComponentData<SimPrototypes>(session).Agent
                : Entity.Null;

            if (existing != Entity.Null && em.Exists(existing))
            {
                if (em.HasComponent<AgentLocomotion>(existing))
                {
                    var loco = em.GetComponentData<AgentLocomotion>(existing);
                    loco.Speed = speed;
                    em.SetComponentData(existing, loco);
                }
                return;
            }

            var prototype = em.CreateEntity();
            em.AddComponent<Prefab>(prototype);
            em.AddComponentData(prototype, new AgentLocomotion { Speed = speed });
            em.AddComponent<AgentAssignment>(prototype);
            em.AddComponent<AgentPlazaIdle>(prototype);
            em.AddComponent<AgentId>(prototype);
            em.AddComponentData(prototype, new AgentType { Kind = AgentKind.Worker });

            EnsureComponent(em, session, new SimPrototypes { Agent = prototype });
        }

        static void PopulateBuildingCatalog(EntityManager em, Entity session, BuildingCatalogAsset catalog)
        {
            EnsureBuffer<BaseBuildingPrototype>(em, session);
            EnsureBuffer<BaseBuildingCatalogCost>(em, session);
            EnsureBuffer<BaseBuildingCatalogRecipe>(em, session);
            EnsureBuffer<BuildingPrototype>(em, session);
            EnsureBuffer<BuildingCatalogCost>(em, session);
            EnsureBuffer<BuildingCatalogRecipe>(em, session);

            var basePrototypes = em.GetBuffer<BaseBuildingPrototype>(session);
            var baseCosts = em.GetBuffer<BaseBuildingCatalogCost>(session);
            var baseRecipes = em.GetBuffer<BaseBuildingCatalogRecipe>(session);
            var prototypes = em.GetBuffer<BuildingPrototype>(session);
            var costs = em.GetBuffer<BuildingCatalogCost>(session);
            var recipes = em.GetBuffer<BuildingCatalogRecipe>(session);

            basePrototypes.Clear();
            baseCosts.Clear();
            baseRecipes.Clear();
            prototypes.Clear();
            costs.Clear();
            recipes.Clear();


            if (catalog == null)
                return;

            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            var prefabs = catalog.Prefabs;
            if (prefabs == null)
                return;

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

                var basePrototype = new BaseBuildingPrototype
                {
                    TypeId = typeKey,
                    WidthClusters = stamp.WidthClusters,
                    DepthRadialRings = stamp.DepthRadialRings,
                    ConstructionDuration = stamp.ConstructionDuration,
                    ConstructionCrewSlots = stamp.ConstructionCrewSlots,
                    WorkplaceSlots = stamp.WorkplaceSlots,
                    ResearchWorkplace = stamp.IsResearchWorkplace ? (byte)1 : (byte)0,
                    RequiresUnlock = stamp.RequiresUnlock ? (byte)1 : (byte)0
                };
                basePrototypes.Add(basePrototype);
                prototypes.Add(basePrototype.ToResolved());

                AddCosts(baseCosts, costs, typeKey, stamp.Costs);
                if (stamp.HasRecipe)
                {
                    AddRecipe(baseRecipes, recipes, typeKey, stamp.RecipeInputs, BuildingRecipeKind.Input);
                    AddRecipe(baseRecipes, recipes, typeKey, stamp.RecipeOutputs, BuildingRecipeKind.Output);
                }
            }
        }

        static void AddCosts(
            DynamicBuffer<BaseBuildingCatalogCost> baseBuffer,
            DynamicBuffer<BuildingCatalogCost> buffer,
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
                var row = new BaseBuildingCatalogCost
                {
                    TypeId = typeKey,
                    ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                    Amount = entry.Amount
                };
                baseBuffer.Add(row);
                buffer.Add(row.ToResolved());
            }
        }

        static void AddRecipe(
            DynamicBuffer<BaseBuildingCatalogRecipe> baseBuffer,
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
                var row = new BaseBuildingCatalogRecipe
                {
                    TypeId = typeKey,
                    Kind = kind,
                    ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                    PerHour = entry.PerHour
                };
                baseBuffer.Add(row);
                buffer.Add(row.ToResolved());
            }
        }

        static void PopulateResourceCatalog(EntityManager em, Entity session, ResourceCatalogAsset catalog, float defaultCap)
        {
            EnsureBuffer<ResourceAmount>(em, session);
            EnsureBuffer<ResourceInfo>(em, session);
            var amounts = em.GetBuffer<ResourceAmount>(session);
            var info = em.GetBuffer<ResourceInfo>(session);

            amounts.Clear();
            info.Clear();


            if (catalog == null)
                return;

            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            var resources = catalog.Resources;
            if (resources == null)
                return;

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
                info.Add(new ResourceInfo
                {
                    ResourceId = key,
                    DisplayName = definition.DisplayName,
                    EnergyValue = definition.EnergyValue,
                    StockCap = cap,
                    CanFeed = definition.CanFeed ? (byte)1 : (byte)0
                });
            }
        }

        static void PopulateTimelineCatalog(EntityManager em, Entity session, TimelineCatalogAsset catalog)
        {
            EnsureBuffer<EraLine>(em, session);
            EnsureBuffer<EraTributeLine>(em, session);
            var eras = em.GetBuffer<EraLine>(session);
            var tribute = em.GetBuffer<EraTributeLine>(session);

            eras.Clear();
            tribute.Clear();

            if (catalog == null)
            {
                EnsureComponent(em, session, new Timeline
                {
                    PreviousMaxLoyalty = 100f,
                    TargetMaxLoyalty = 100f
                });
                EnsureComponent(em, session, GodLoyalty.Full());
                return;
            }

            var specs = catalog.Eras;
            var firstMax = 100f;
            if (specs != null)
            {
                for (var i = 0; i < specs.Length; i++)
                {
                    var spec = specs[i];
                    if (!ContentId.TryEncode(ContentId.Normalize(spec.eraId, spec.displayName), out var id))
                        continue;

                    var days = spec.durationDays > 0 ? spec.durationDays : 1;
                    var max = spec.maxLoyalty;
                    if (max < 0f) max = 0f;
                    else if (max > 100f) max = 100f;

                    if (eras.Length == 0)
                        firstMax = max;

                    var name = string.IsNullOrWhiteSpace(spec.displayName) ? spec.eraId : spec.displayName;
                    var summary = spec.summary ?? string.Empty;
                    if (summary.Length > 500)
                        summary = summary.Substring(0, 500);

                    eras.Add(new EraLine
                    {
                        EraId = id,
                        DisplayName = name,
                        DurationDays = days,
                        MaxLoyalty = max,
                        TributeEnergyMul = spec.tributeEnergyMultiplier > 0.0001f ? spec.tributeEnergyMultiplier : 1f,
                        LoyaltyPerEnergy = spec.loyaltyPerEnergy < 0f ? 0f : spec.loyaltyPerEnergy,
                        Summary = summary
                    });

                    var eraIndex = eras.Length - 1;
                    var gifts = spec.tribute;
                    if (gifts != null)
                    {
                        for (var t = 0; t < gifts.Length; t++)
                        {
                            var res = gifts[t];
                            if (res == null || !ContentId.TryEncode(res.ResourceId, out var resId))
                                continue;

                            tribute.Add(new EraTributeLine
                            {
                                EraIndex = eraIndex,
                                ResourceId = resId
                            });
                        }
                    }
                }
            }

            EnsureComponent(em, session, new Timeline
            {
                EraIndex = 0,
                PreviousMaxLoyalty = firstMax,
                TargetMaxLoyalty = firstMax
            });

            var loyalty = GodLoyalty.Full();
            loyalty.Value = firstMax;
            loyalty.EffectiveMax = firstMax;
            EnsureComponent(em, session, loyalty);
        }

        static void EnsureHeadquarters(EntityManager em, Entity session)
        {
            using var hqQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Headquarters>());
            Entity hqEntity;
            if (hqQuery.IsEmptyIgnoreFilter)
            {
                hqEntity = em.CreateEntity();
                em.AddComponent<Headquarters>(hqEntity);
                em.AddBuffer<PyramidFeedLine>(hqEntity);
                em.AddComponentData(hqEntity, LocalTransform.FromPosition(float3.zero));
            }
            else
            {
                hqEntity = hqQuery.GetSingletonEntity();
            }

            var feed = EnsureBuffer<PyramidFeedLine>(em, hqEntity);
            if (em.HasBuffer<ResourceInfo>(session))
            {
                var info = em.GetBuffer<ResourceInfo>(session);
                for (var i = 0; i < info.Length; i++)
                {
                    if (info[i].CanFeed != 0 && PyramidFeed.IndexOf(feed, info[i].ResourceId) < 0)
                    {
                        feed.Add(new PyramidFeedLine
                        {
                            ResourceId = info[i].ResourceId,
                            PerHour = 0f
                        });
                    }
                }
            }
        }
    }
}