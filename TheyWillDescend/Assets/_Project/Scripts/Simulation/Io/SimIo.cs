using System.Collections.Generic;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
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

        public static void SetCityCenter(float3 center)
        {
            if (!TrySession(out var em, out var session))
                return;

            var grid = em.GetComponentData<CityGrid>(session);
            grid.Center = center;
            em.SetComponentData(session, grid);
        }

        public static bool TryGetCityGrid(out CityGrid grid)
        {
            grid = default;
            if (!TrySession(out var em, out var session))
                return false;
            grid = em.GetComponentData<CityGrid>(session);
            return grid.Ready != 0 && grid.Config.IsValid;
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
