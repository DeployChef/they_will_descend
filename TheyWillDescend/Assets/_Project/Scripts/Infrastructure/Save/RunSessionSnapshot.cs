using System;
using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Presentation.Agents;
using _Project.Scripts.Presentation.City;
using _Project.Scripts.Shell;
using _Project.Scripts.Simulation.Agents;
using _Project.Scripts.Simulation.Time;
using Unity.Collections;
using Unity.Entities;

namespace _Project.Scripts.Infrastructure.Save
{
    /// <summary>
    /// Capture/apply run snapshot. Reads ECS write model; rebuilds presentation via spawners.
    /// </summary>
    public static class RunSessionSnapshot
    {
        public static RunSnapshot Capture(BuildPlacementController placement)
        {
            var snapshot = new RunSnapshot();
            var gate = SimGate.Active;
            if (gate != null)
            {
                snapshot.speed = gate.Speed;
                snapshot.playerPaused = gate.PlayerPaused;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                using var timeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<GameTime>());
                if (!timeQuery.IsEmptyIgnoreFilter)
                {
                    var time = timeQuery.GetSingleton<GameTime>();
                    snapshot.day = time.Day;
                    snapshot.elapsedInDay = time.ElapsedInDay;
                    snapshot.dayDuration = time.DayDuration;
                }

                using var agentQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<AgentPosition>(),
                    ComponentType.ReadOnly<CircleWalk>(),
                    ComponentType.ReadOnly<AgentPrefabId>());
                var positions = agentQuery.ToComponentDataArray<AgentPosition>(Allocator.Temp);
                var walks = agentQuery.ToComponentDataArray<CircleWalk>(Allocator.Temp);
                var ids = agentQuery.ToComponentDataArray<AgentPrefabId>(Allocator.Temp);
                snapshot.version = 2;
                snapshot.agents = new AgentSnapshot[positions.Length];
                for (var i = 0; i < positions.Length; i++)
                {
                    var position = positions[i];
                    var walk = walks[i];
                    snapshot.agents[i] = new AgentSnapshot
                    {
                        prefabId = ids[i].Value.ToString(),
                        posX = position.Value.x,
                        posY = position.Value.y,
                        posZ = position.Value.z,
                        fwdX = position.Facing.x,
                        fwdY = position.Facing.y,
                        fwdZ = position.Facing.z,
                        centerX = walk.Center.x,
                        centerY = walk.Center.y,
                        centerZ = walk.Center.z,
                        radius = walk.Radius,
                        speed = walk.Speed,
                        direction = walk.Direction,
                        angleRadians = walk.AngleRadians
                    };
                }

                positions.Dispose();
                walks.Dispose();
                ids.Dispose();
            }

            snapshot.buildings = placement != null
                ? placement.CopyPlaced()
                : Array.Empty<BuildingSnapshot>();

            return snapshot;
        }

        public static void Apply(
            RunSnapshot snapshot,
            AgentSpawner spawner,
            BuildPlacementController placement)
        {
            if (snapshot == null)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
                world.EntityManager.CompleteAllTrackedJobs();

            var gate = SimGate.Active;
            gate?.RestoreFromSnapshot(snapshot.speed, playerPaused: true);

            spawner?.ClearSpawned();
            placement?.ClearPlaced();
            DestroyLeftoverAgentEntities();

            ApplyGameTime(snapshot);

            if (spawner != null && snapshot.agents != null)
            {
                for (var i = 0; i < snapshot.agents.Length; i++)
                    spawner.SpawnFromSnapshot(snapshot.agents[i], snapshot.version);
            }

            placement?.RestorePlaced(snapshot.buildings);
            GameLog.Info(
                $"Applied snapshot: day {snapshot.day}, agents {snapshot.agents?.Length ?? 0}, buildings {snapshot.buildings?.Length ?? 0}.");
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
            em.SetComponentData(entity, new GameTime
            {
                Day = snapshot.day,
                ElapsedInDay = snapshot.elapsedInDay,
                DayDuration = snapshot.dayDuration > 0f ? snapshot.dayDuration : 5f
            });
        }

        static void DestroyLeftoverAgentEntities()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;
            using var agentQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<AgentPrefabId>());
            if (agentQuery.IsEmptyIgnoreFilter)
                return;

            em.DestroyEntity(agentQuery);
        }
    }
}
