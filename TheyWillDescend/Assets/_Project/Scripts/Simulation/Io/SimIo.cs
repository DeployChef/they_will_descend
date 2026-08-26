using System.Collections.Generic;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// Presentation view of one catalog row. Ghost mesh comes from
    /// <see cref="TheyWillDescend.Simulation.Content.BuildingCatalogAsset"/>, not this struct.
    /// </summary>
    public struct BuildingCatalogEntry
    {
        public string TypeId;
        public int WidthClusters;
        public int DepthRadialRings;
        public float MeshSize;
        public float ConstructionDuration;
        public int WorkplaceSlots;
        public string DisplayName;

        public BuildingFootprint Footprint => new()
        {
            WidthClusters = WidthClusters,
            DepthRadialRings = DepthRadialRings
        };
    }

    public struct BuildingInspect
    {
        public int BuildingId;
        public string TypeId;
        public string DisplayName;
        public int WorkplaceSlots;
        public Workplace Workplace;
        public bool Constructing;
    }

    public struct ResourceView
    {
        public string ResourceId;
        public string DisplayName;
        public float Amount;
    }

    /// <summary>
    /// UI/save enqueue intents and pull numbers. They do not write stocks, poses, or step the world.
    /// </summary>
    public static class SimIo
    {
        static World _cachedWorld;
        static Entity _session;

        public static bool TryGetSimControl(out SimControl control)
        {
            control = default;
            if (!TrySession(out var em, out var session) || !em.HasComponent<SimControl>(session))
                return false;
            control = em.GetComponentData<SimControl>(session);
            return true;
        }

        public static bool TryEnqueueSessionInGame(bool inGame)
        {
            return TryEnqueueClock(new SimClockCommand
            {
                Kind = SimClockCommandKind.SetSessionInGame,
                Value = inGame ? 1 : 0
            });
        }

        public static bool TryEnqueueTogglePlayerPause()
        {
            return TryEnqueueClock(new SimClockCommand { Kind = SimClockCommandKind.TogglePlayerPause });
        }

        public static bool TryEnqueueSimSpeed(int speed)
        {
            return TryEnqueueClock(new SimClockCommand
            {
                Kind = SimClockCommandKind.SetSpeed,
                Value = speed
            });
        }

        public static bool TryEnqueueBuildLocked(bool locked)
        {
            return TryEnqueueClock(new SimClockCommand
            {
                Kind = SimClockCommandKind.SetBuildLocked,
                Value = locked ? 1 : 0
            });
        }

        public static bool TryEnqueueRestoreClock(int speed, bool playerPaused)
        {
            return TryEnqueueClock(new SimClockCommand
            {
                Kind = SimClockCommandKind.Restore,
                Value = speed,
                Secondary = playerPaused ? 1 : 0
            });
        }

        static bool TryEnqueueClock(in SimClockCommand command)
        {
            if (!TrySession(out var em, out var session))
                return false;
            if (!em.HasBuffer<SimClockCommand>(session))
                em.AddBuffer<SimClockCommand>(session);
            em.GetBuffer<SimClockCommand>(session).Add(command);
            ConsumeSimClockCommandsSystem.Run(em);
            return true;
        }

        public static bool TryGetCityGrid(out CityGrid grid)
        {
            grid = default;
            if (!TrySession(out var em, out var session))
                return false;
            grid = em.GetComponentData<CityGrid>(session);
            return grid.Ready != 0 && grid.Config.IsValid;
        }

        public static bool TryGetCityCenter(out float3 center)
        {
            if (!TryGetCityGrid(out var grid))
            {
                center = default;
                return false;
            }

            center = grid.Center;
            return true;
        }

        public static bool TryGetGameTime(out GameTime time)
        {
            time = default;
            if (!TryManager(out var em))
                return false;

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameTime>());
            if (query.IsEmptyIgnoreFilter)
                return false;
            time = query.GetSingleton<GameTime>();
            return true;
        }

        public static bool OverlapsOccupied(List<(int cluster, int radial)> clusters)
        {
            if (clusters == null || clusters.Count == 0)
                return false;
            if (!TrySession(out var em, out var session))
                return false;

            var occupied = em.GetBuffer<OccupiedCell>(session);
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

        public static int CopyResourceLedger(List<ResourceView> dst)
        {
            if (dst == null)
                return 0;
            dst.Clear();
            if (!TrySession(out var em, out var session)
                || !em.HasBuffer<ResourceAmount>(session)
                || !em.HasBuffer<ResourceInfo>(session))
                return 0;

            var stock = em.GetBuffer<ResourceAmount>(session);
            var info = em.GetBuffer<ResourceInfo>(session);
            for (var i = 0; i < info.Length; i++)
            {
                var row = info[i];
                dst.Add(new ResourceView
                {
                    ResourceId = row.ResourceId.ToString(),
                    DisplayName = row.DisplayName.ToString(),
                    Amount = ResourceLedger.Get(stock, row.ResourceId)
                });
            }

            return dst.Count;
        }

        public static void SetResourceAmount(string resourceId, float amount)
        {
            var id = ContentId.EncodeOrEmpty(resourceId);
            if (id.IsEmpty || !TrySession(out var em, out var session) || !em.HasBuffer<ResourceAmount>(session))
                return;
            ResourceLedger.Set(em.GetBuffer<ResourceAmount>(session), id, amount);
        }

        public static bool CanAfford(string typeId)
        {
            var id = ContentId.EncodeOrEmpty(typeId);
            if (id.IsEmpty || !TrySession(out var em, out var session) || !em.HasBuffer<BuildingCost>(session))
                return true;
            if (!em.HasBuffer<ResourceAmount>(session))
                return !HasCost(em.GetBuffer<BuildingCost>(session), id);

            var costs = em.GetBuffer<BuildingCost>(session);
            var stock = em.GetBuffer<ResourceAmount>(session);
            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i];
                if (cost.TypeId != id || cost.Amount <= 0.0001f)
                    continue;
                if (!ResourceLedger.Has(stock, cost.ResourceId, cost.Amount))
                    return false;
            }

            return true;
        }

        public static string FormatBuildingCost(string typeId)
        {
            var id = ContentId.EncodeOrEmpty(typeId);
            if (id.IsEmpty || !TrySession(out var em, out var session) || !em.HasBuffer<BuildingCost>(session))
                return string.Empty;

            var costs = em.GetBuffer<BuildingCost>(session);
            var names = em.HasBuffer<ResourceInfo>(session) ? em.GetBuffer<ResourceInfo>(session) : default;
            var parts = new List<string>(4);
            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i];
                if (cost.TypeId != id || cost.Amount <= 0.0001f)
                    continue;
                parts.Add($"{(int)math.ceil(cost.Amount)} {ResourceName(names, cost.ResourceId)}");
            }

            return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
        }

        static bool HasCost(DynamicBuffer<BuildingCost> costs, in FixedString64Bytes typeId)
        {
            for (var i = 0; i < costs.Length; i++)
            {
                if (costs[i].TypeId == typeId && costs[i].Amount > 0.0001f)
                    return true;
            }

            return false;
        }

        static string ResourceName(DynamicBuffer<ResourceInfo> info, in FixedString64Bytes resourceId)
        {
            if (info.IsCreated)
            {
                for (var i = 0; i < info.Length; i++)
                {
                    if (info[i].ResourceId == resourceId)
                        return info[i].DisplayName.ToString();
                }
            }

            return resourceId.ToString();
        }

        public static bool TryGetWorkplace(int buildingId, out Workplace workplace, out bool constructing)
        {
            workplace = default;
            constructing = false;
            if (buildingId <= 0 || !TryManager(out var em))
                return false;

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            for (var i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].Id != buildingId)
                    continue;
                constructing = em.HasComponent<Construction>(entities[i]);
                if (em.HasComponent<Workplace>(entities[i]))
                    workplace = em.GetComponentData<Workplace>(entities[i]);
                return true;
            }

            return false;
        }

        public static int CountIdleWorkers()
        {
            if (!TryManager(out var em))
                return 0;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<AgentId>(),
                ComponentType.ReadOnly<AgentAssignment>());
            using var assignments = query.ToComponentDataArray<AgentAssignment>(Allocator.Temp);
            var idle = 0;
            for (var i = 0; i < assignments.Length; i++)
            {
                if (assignments[i].WorkplaceBuildingId == 0)
                    idle++;
            }

            return idle;
        }

        public static bool TryEnqueueAssignWorker(int buildingId, int agentId = 0)
        {
            if (buildingId <= 0 || !TrySession(out var em, out var session))
                return false;
            em.GetBuffer<AssignWorkerCommand>(session).Add(new AssignWorkerCommand
            {
                BuildingId = buildingId,
                AgentId = agentId
            });
            return true;
        }

        public static bool TryEnqueueUnassignWorker(int buildingId)
        {
            if (buildingId <= 0 || !TrySession(out var em, out var session))
                return false;
            em.GetBuffer<UnassignWorkerCommand>(session).Add(new UnassignWorkerCommand { BuildingId = buildingId });
            return true;
        }

        public static bool TryEnqueueSpawn(in SpawnAgentCommand command)
        {
            if (!TrySession(out var em, out var session))
                return false;
            em.GetBuffer<SpawnAgentCommand>(session).Add(command);
            return true;
        }

        public static bool TryEnqueuePlaceBuilding(in PlaceBuildingCommand command)
        {
            if (!TrySession(out var em, out var session))
                return false;
            em.GetBuffer<PlaceBuildingCommand>(session).Add(command);
            return true;
        }

        public static bool TryGetBuilding(string typeId, out BuildingCatalogEntry entry)
        {
            entry = default;
            var id = ContentId.EncodeOrEmpty(typeId);
            if (id.IsEmpty || !TrySession(out var em, out var session) || !em.HasBuffer<BuildingPrototype>(session))
                return false;
            var catalog = em.GetBuffer<BuildingPrototype>(session);
            if (!BuildingCatalog.TryResolve(catalog, id, 0, 0, out var prototype))
                return false;
            entry = ToEntry(prototype);
            return true;
        }

        public static int CopyBuildingCatalog(List<BuildingCatalogEntry> dst)
        {
            if (dst == null)
                return 0;
            dst.Clear();
            if (!TrySession(out var em, out var session) || !em.HasBuffer<BuildingPrototype>(session))
                return 0;

            var catalog = em.GetBuffer<BuildingPrototype>(session);
            for (var i = 0; i < catalog.Length; i++)
            {
                var prototype = catalog[i];
                if (prototype.Prefab == Entity.Null || prototype.TypeId.IsEmpty)
                    continue;
                dst.Add(ToEntry(prototype));
            }

            return dst.Count;
        }

        static BuildingCatalogEntry ToEntry(in BuildingPrototype prototype)
        {
            return new BuildingCatalogEntry
            {
                TypeId = prototype.TypeId.ToString(),
                WidthClusters = prototype.WidthClusters,
                DepthRadialRings = prototype.DepthRadialRings,
                MeshSize = prototype.MeshSize,
                ConstructionDuration = prototype.ConstructionDuration,
                WorkplaceSlots = prototype.WorkplaceSlots,
                DisplayName = prototype.DisplayName.ToString()
            };
        }

        public static bool TryGetBuildingInspect(int buildingId, out BuildingInspect inspect)
        {
            inspect = default;
            if (buildingId <= 0 || !TryManager(out var em))
                return false;

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            for (var i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].Id != buildingId)
                    continue;

                var entity = entities[i];
                var building = buildings[i];
                TryGetBuilding(building.TypeId.ToString(), out var catalog);
                var workplace = em.HasComponent<Workplace>(entity)
                    ? em.GetComponentData<Workplace>(entity)
                    : default;
                inspect = new BuildingInspect
                {
                    BuildingId = buildingId,
                    TypeId = building.TypeId.ToString(),
                    DisplayName = string.IsNullOrEmpty(catalog.DisplayName)
                        ? $"Building {building.TypeId}"
                        : catalog.DisplayName,
                    WorkplaceSlots = catalog.WorkplaceSlots > 0
                        ? catalog.WorkplaceSlots
                        : (em.HasComponent<Workplace>(entity) ? 1 : 0),
                    Workplace = workplace,
                    Constructing = em.HasComponent<Construction>(entity)
                };
                return true;
            }

            return false;
        }

        public static bool TryRequestDespawnAllAgents()
        {
            if (!TrySession(out var em, out var session))
                return false;
            var data = em.GetComponentData<SimBridge>(session);
            data.DespawnAllAgents = 1;
            em.SetComponentData(session, data);
            return true;
        }

        public static bool TryRequestDespawnAllBuildings()
        {
            if (!TrySession(out var em, out var session))
                return false;
            var data = em.GetComponentData<SimBridge>(session);
            data.DespawnAllBuildings = 1;
            em.SetComponentData(session, data);
            return true;
        }

        public static void PlaybackCommands()
        {
            if (!TryManager(out var em))
                return;
            SimCommandPlayback.Run(em);
        }

        static bool TrySession(out EntityManager em, out Entity session)
        {
            session = default;
            if (!TryManager(out em))
            {
                _cachedWorld = null;
                _session = default;
                return false;
            }

            if (_cachedWorld == em.World && em.Exists(_session))
            {
                session = _session;
                return true;
            }

            if (!SimBridgeAccess.TryGet(em, out session))
            {
                _cachedWorld = null;
                _session = default;
                return false;
            }

            _cachedWorld = em.World;
            _session = session;
            return true;
        }

        static bool TryManager(out EntityManager em)
        {
            em = default;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;
            em = world.EntityManager;
            return true;
        }
    }
}
