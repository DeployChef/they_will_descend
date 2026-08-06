using _Project.Scripts.Infrastructure.Logging;
using Unity.Entities;

namespace _Project.Scripts.Simulation.Time
{
    partial struct AdvanceGameTimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Singleton = в мире ровно один GameTime (мы так договорились)
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
