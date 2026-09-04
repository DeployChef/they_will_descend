using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Turns pending worker count into SpawnAgentCommand after the run publisher
    /// has applied scenario + difficulty.
    /// </summary>
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumeDespawnBuildingsSystem))]
    [UpdateBefore(typeof(ConsumeSpawnAgentCommandsSystem))]
    public partial struct ConsumePendingScenarioSpawnsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PendingScenarioSpawns>();
            state.RequireForUpdate<CityGrid>();
            state.RequireForUpdate<SimSession>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!Application.isPlaying)
                return;
            Run(state.EntityManager);
        }

        public static void Run(EntityManager em)
        {
            if (!SimSessionAccess.TryGet(em, out var session))
                return;
            if (!em.HasComponent<PendingScenarioSpawns>(session)
                || !em.HasComponent<CityGrid>(session)
                || !em.HasComponent<SimSession>(session))
                return;
            if (!em.GetComponentData<SimSession>(session).AcceptsSetupCommands)
                return;

            var pending = em.GetComponentData<PendingScenarioSpawns>(session);
            if (pending.Workers <= 0)
                return;

            var center = em.GetComponentData<CityGrid>(session).Center;
            if (!em.HasBuffer<SpawnAgentCommand>(session))
                return;
            var commands = em.GetBuffer<SpawnAgentCommand>(session);
            var count = pending.Workers;
            for (var i = 0; i < count; i++)
            {
                var turns = count == 1 ? 0f : i / (float)count;
                var angle = turns * 2f * math.PI;
                var radius = 4f + i % 4 * 0.7f;
                var position = new float3(
                    center.x + math.cos(angle) * radius,
                    center.y,
                    center.z + math.sin(angle) * radius);
                var facing = new float3(-math.sin(angle), 0f, math.cos(angle));
                commands.Add(new SpawnAgentCommand
                {
                    Position = position,
                    Facing = facing,
                    Speed = 0f,
                    HasPose = 1,
                    PlazaWalking = 1,
                    PlazaAngle = angle,
                    PlazaRadius = radius,
                    PlazaTimer = 2.5f,
                    Kind = AgentKind.Worker
                });
            }

            pending.Workers = 0;
            em.SetComponentData(session, pending);
        }
    }
}
