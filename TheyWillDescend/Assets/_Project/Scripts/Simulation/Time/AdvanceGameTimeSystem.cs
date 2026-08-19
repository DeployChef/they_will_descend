using TheyWillDescend.Simulation.Io;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Time
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
                if (SystemAPI.HasSingleton<SimBridge>())
                    SystemAPI.GetSingletonBuffer<DayChangedEvent>().Add(
                        new DayChangedEvent { Day = time.Day });
            }
        }
    }
}
