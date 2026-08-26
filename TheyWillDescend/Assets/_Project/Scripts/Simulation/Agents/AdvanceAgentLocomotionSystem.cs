using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Walks toward Target when Moving. Behaviors write Target; this only steers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AdvancePlazaIdleSystem))]
    public partial struct AdvanceAgentLocomotionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<AgentLocomotion>();
            state.RequireForUpdate<AgentId>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var control = SystemAPI.GetSingleton<SimControl>();
            if (!control.IsRunning)
                return;
            var dt = control.DeltaTime;
            if (dt <= 0f)
                return;

            foreach (var (locomotion, transform) in
                     SystemAPI.Query<RefRW<AgentLocomotion>, RefRW<LocalTransform>>()
                         .WithAll<AgentId>())
            {
                var motor = locomotion.ValueRO;
                if (motor.Moving == 0)
                    continue;

                var lt = transform.ValueRO;
                var delta = motor.Target - lt.Position;
                delta.y = 0f;
                var distance = math.length(delta);
                if (distance <= 0.0001f)
                {
                    motor.Moving = 0;
                    locomotion.ValueRW = motor;
                    continue;
                }

                var speed = motor.Speed > 0.001f ? motor.Speed : 2f;
                var step = math.min(speed * dt, distance);
                var direction = delta / distance;
                lt.Position += direction * step;
                lt.Rotation = quaternion.LookRotationSafe(direction, math.up());
                transform.ValueRW = lt;
            }
        }
    }
}
