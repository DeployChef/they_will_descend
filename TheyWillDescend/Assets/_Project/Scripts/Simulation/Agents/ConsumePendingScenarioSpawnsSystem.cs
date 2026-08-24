using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Io;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// First Play tick: turn baked worker count into SpawnAgentCommand.
    /// Skips Edit Mode so Live Conversion does not re-apply the queue.
    /// </summary>
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateBefore(typeof(ConsumeSpawnAgentCommandsSystem))]
    public partial struct ConsumePendingScenarioSpawnsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PendingScenarioSpawns>();
            state.RequireForUpdate<CityGrid>();
            state.RequireForUpdate<SimBridge>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!Application.isPlaying)
                return;

            var session = SystemAPI.GetSingletonEntity<PendingScenarioSpawns>();
            var pending = SystemAPI.GetComponent<PendingScenarioSpawns>(session);
            if (pending.Workers <= 0)
                return;

            var center = SystemAPI.GetComponent<CityGrid>(session).Center;
            var commands = SystemAPI.GetBuffer<SpawnAgentCommand>(session);
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
                    Speed = 2f,
                    HasPose = 1,
                    PlazaAngle = angle,
                    PlazaRadius = radius,
                    PlazaTimer = 2.5f,
                    Kind = AgentKind.Worker
                });
            }

            pending.Workers = 0;
            SystemAPI.SetComponent(session, pending);
        }
    }
}
