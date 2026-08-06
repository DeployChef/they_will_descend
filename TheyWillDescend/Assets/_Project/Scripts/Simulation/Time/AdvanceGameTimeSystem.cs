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

            if (simControl.Mode != SimRunMode.Running)
                return;

            if (!SystemAPI.TryGetSingletonRW<GameTime>(out var timeRW))
                return;

            ref var time = ref timeRW.ValueRW;
            time.ElapsedInDay += SystemAPI.Time.DeltaTime;

            while (time.ElapsedInDay >= time.DayDuration)
            {
                time.ElapsedInDay -= time.DayDuration;
                time.Day += 1;
                GameLog.Info(LogChannel.Time, $"Day {time.Day}");
            }
        }
    }
}
