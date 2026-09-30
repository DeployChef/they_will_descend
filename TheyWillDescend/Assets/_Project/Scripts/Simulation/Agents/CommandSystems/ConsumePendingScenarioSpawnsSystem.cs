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

            var grid = em.GetComponentData<CityGrid>(session);
            var center = grid.Center;
            var count = pending.Workers;
            for (var i = 0; i < count; i++)
            {
                var turns = count == 1 ? 0f : i / (float)count;
                var angle = turns * 2f * math.PI;
                var radius = PlazaRing.Lane(grid.Config, i);
                var position = new float3(
                    center.x + math.cos(angle) * radius,
                    center.y,
                    center.z + math.sin(angle) * radius);
                var direction = PlazaRing.PickDirection(i * 17 + 3);
                var walking = (byte)(i % 4 == 0 ? 0 : 1);
                var tangent = new float3(-math.sin(angle), 0f, math.cos(angle)) * direction;
                var inward = new float3(-math.cos(angle), 0f, -math.sin(angle));

                var reqEntity = em.CreateEntity();
                em.AddComponentData(reqEntity, new SpawnAgentRequest
                {
                    Position = position,
                    Facing = walking != 0 ? tangent : inward,
                    Speed = 0f,
                    HasPose = 1,
                    PlazaWalking = walking,
                    PlazaDirection = direction,
                    PlazaAngle = angle,
                    PlazaRadius = radius,
                    PlazaTimer = walking != 0 ? 3f + (i % 5) * 0.9f : 1.5f + (i % 3) * 0.5f,
                    Kind = AgentKind.Worker
                });
            }


            pending.Workers = 0;
            em.SetComponentData(session, pending);
        }
    }
}
