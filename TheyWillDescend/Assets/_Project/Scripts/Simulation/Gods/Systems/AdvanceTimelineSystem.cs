using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Gods
{
    /// <summary>
    /// Era boundary at day+hour. Loyalty cap lerps 24h from that instant.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AdvanceGameTimeSystem))]
    public partial struct AdvanceTimelineSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<Timeline>();
            state.RequireForUpdate<GodLoyalty>();
            state.RequireForUpdate<PyramidConfig>();
            state.RequireForUpdate<EraLine>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var control = SystemAPI.GetSingleton<SimControl>();
            if (!control.IsRunning)
                return;

            var time = SystemAPI.GetSingleton<GameTime>();
            var config = SystemAPI.GetSingleton<PyramidConfig>();
            var eras = SystemAPI.GetSingletonBuffer<EraLine>(true);
            if (eras.Length == 0)
                return;

            ref var timeline = ref SystemAPI.GetSingletonRW<Timeline>().ValueRW;
            ref var loyalty = ref SystemAPI.GetSingletonRW<GodLoyalty>().ValueRW;

            while (timeline.EraIndex + 1 < eras.Length)
            {
                EraClock.StartOfEra(
                    eras,
                    config.EraChangeHour,
                    time.DayDuration,
                    timeline.EraIndex + 1,
                    out var day,
                    out var elapsed);
                if (!EraClock.Reached(time, day, elapsed))
                    break;

                timeline.EraIndex += 1;
                timeline.EraStartDay = time.Day;
                timeline.EraStartElapsed = time.ElapsedInDay;
                timeline.PreviousMaxLoyalty = loyalty.EffectiveMax;
                timeline.TargetMaxLoyalty = eras[timeline.EraIndex].MaxLoyalty;
            }

            var hours = EraClock.HoursSince(time, timeline.EraStartDay, timeline.EraStartElapsed);
            var t = hours <= 0f ? 0f : math.saturate(hours / 24f);
            if (timeline.EraIndex == 0 && timeline.EraStartDay == 0 && timeline.EraStartElapsed <= 0.0001f
                && math.abs(timeline.PreviousMaxLoyalty - timeline.TargetMaxLoyalty) < 0.0001f)
                t = 1f;

            loyalty.EffectiveMax = math.lerp(timeline.PreviousMaxLoyalty, timeline.TargetMaxLoyalty, t);
            loyalty.ClampToEffectiveMax();
        }
    }
}
