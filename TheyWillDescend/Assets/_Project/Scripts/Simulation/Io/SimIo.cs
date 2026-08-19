using System.Collections.Generic;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// Only managed entry into simulation. UI/save enqueue data; they do not write stocks or poses.
    /// </summary>
    public static class SimIo
    {
        public static void SetClock(SimRunMode mode, int speed, float unscaledDeltaTime)
        {
            if (!TryManager(out var em))
                return;

            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<SimControl>());
            if (query.IsEmptyIgnoreFilter)
                return;

            var entity = query.GetSingletonEntity();
            var dt = mode == SimRunMode.Running ? unscaledDeltaTime * speed : 0f;
            em.SetComponentData(entity, new SimControl
            {
                Mode = mode,
                Speed = speed,
                DeltaTime = dt
            });
        }

        public static void SetCityGrid(in RadialGridConfig config, float3 center)
        {
            if (!TryManager(out var em))
                return;

            var entity = SimBridgeAccess.GetOrCreateCityGrid(em);
            var grid = em.GetComponentData<CityGrid>(entity);
            grid.Config = config;
            grid.Center = center;
            grid.Ready = 1;
            em.SetComponentData(entity, grid);
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
            if (!TryManager(out var em))
                return false;

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<OccupiedCell>());
            if (query.IsEmptyIgnoreFilter)
                return false;

            var entity = query.GetSingletonEntity();
            var occupied = em.GetBuffer<OccupiedCell>(entity);
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
            if (!TryManager(out var em))
                return false;
            var bridge = SimBridgeAccess.GetOrCreate(em);
            em.GetBuffer<SpawnAgentCommand>(bridge).Add(command);
            return true;
        }

        public static bool TryEnqueuePlaceBuilding(in PlaceBuildingCommand command)
        {
            if (!TryManager(out var em))
                return false;
            var bridge = SimBridgeAccess.GetOrCreate(em);
            em.GetBuffer<PlaceBuildingCommand>(bridge).Add(command);
            return true;
        }

        public static bool TryRequestDespawnAllAgents()
        {
            if (!TryManager(out var em))
                return false;
            var bridge = SimBridgeAccess.GetOrCreate(em);
            var data = em.GetComponentData<SimBridge>(bridge);
            data.DespawnAllAgents = 1;
            em.SetComponentData(bridge, data);
            return true;
        }

        public static bool TryRequestDespawnAllBuildings()
        {
            if (!TryManager(out var em))
                return false;
            var bridge = SimBridgeAccess.GetOrCreate(em);
            var data = em.GetComponentData<SimBridge>(bridge);
            data.DespawnAllBuildings = 1;
            em.SetComponentData(bridge, data);
            return true;
        }

        public static void Flush()
        {
            if (!TryManager(out var em))
                return;
            SimCommandProcessor.Run(em);
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
