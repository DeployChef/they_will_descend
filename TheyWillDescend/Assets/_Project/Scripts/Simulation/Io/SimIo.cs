using System.Collections.Generic;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// UI/save enqueue intents and pull numbers. They do not write Money, poses, or step the world.
    /// PlaybackCommands is load-only.
    /// </summary>
    public static class SimIo
    {
        static World _cachedWorld;
        static Entity _session;

        public static void SetClock(SimRunMode mode, int speed, float unscaledDeltaTime)
        {
            if (!TrySession(out var em, out var session))
                return;

            var dt = mode == SimRunMode.Running ? unscaledDeltaTime * speed : 0f;
            em.SetComponentData(session, new SimControl
            {
                Mode = mode,
                Speed = speed,
                DeltaTime = dt
            });
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

        public static bool TryGetStock(out ResourceStock stock)
        {
            stock = default;
            if (!TryManager(out var em))
                return false;

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ResourceStock>());
            if (query.IsEmptyIgnoreFilter)
                return false;
            stock = query.GetSingleton<ResourceStock>();
            return true;
        }

        public static void SetStock(in ResourceStock stock)
        {
            if (!TryManager(out var em))
                return;

            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<ResourceStock>());
            if (query.IsEmptyIgnoreFilter)
                return;
            em.SetComponentData(query.GetSingletonEntity(), stock);
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
