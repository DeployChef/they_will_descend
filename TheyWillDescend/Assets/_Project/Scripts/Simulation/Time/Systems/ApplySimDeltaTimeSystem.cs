using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Time
{
    /// <summary>
    /// Writes frame length × Speed into <see cref="SimControl.DeltaTime"/>.
    /// Does not zero dt on pause — systems skip work when Mode is not Running.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CommandSystemGroup), OrderLast = true)]
    public partial struct ApplySimDeltaTimeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonRW<SimControl>(out var control))
                return;

            var speed = control.ValueRO.Speed;
            if (speed < 1)
                speed = 1;
            control.ValueRW.DeltaTime = SystemAPI.Time.DeltaTime * speed;
        }
    }
}
