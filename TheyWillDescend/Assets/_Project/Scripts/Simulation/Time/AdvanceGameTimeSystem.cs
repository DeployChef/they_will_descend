using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Simulation.Session;
using Unity.Entities;

namespace _Project.Scripts.Simulation.Time
{
    partial struct AdvanceGameTimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<SimControl>(out var simControl))
                return;

            if (!SystemAPI.TryGetSingletonRW<GameTime>(out var timeRW))
                return;

            var dt = simControl.DeltaTime;
            if (dt <= 0f)
                return;

            ref var time = ref timeRW.ValueRW;
            time.ElapsedInDay += dt;

            while (time.ElapsedInDay >= time.DayDuration)
            {
                time.ElapsedInDay -= time.DayDuration;
                time.Day += 1;
                GameLog.Info($"Day {time.Day}");
            }
        }
    }
}
