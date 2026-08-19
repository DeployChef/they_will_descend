using TheyWillDescend.Simulation.Session;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Time
{
    /// <summary>
    /// Day clock. HUD pulls GameTime; no DayChanged event.
    /// </summary>
    [Unity.Burst.BurstCompile]
    public partial struct AdvanceGameTimeSystem : ISystem
    {
        [Unity.Burst.BurstCompile]
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
            }
        }
    }
}
