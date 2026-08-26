using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Unassigned: stand or walk a ring around CityGrid.Center.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AdvanceAgentCommuteSystem))]
    public partial struct AdvancePlazaIdleSystem : ISystem
    {
        const float RadiusMin = 4f;
        const float RadiusMax = 7f;
        const float WalkAngularSpeed = 0.55f;
        const float FarDistance = 9f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<CityGrid>();
            state.RequireForUpdate<AgentPlazaIdle>();
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

            var center = SystemAPI.GetSingleton<CityGrid>().Center;

            foreach (var (idleRef, assignment, locomotion, transform, id) in
                     SystemAPI.Query<RefRW<AgentPlazaIdle>, RefRO<AgentAssignment>, RefRW<AgentLocomotion>,
                         RefRO<LocalTransform>, RefRO<AgentId>>())
            {
                if (assignment.ValueRO.WorkplaceBuildingId != 0)
                    continue;

                var idle = idleRef.ValueRO;
                var motor = locomotion.ValueRO;
                TickPlaza(
                    ref idle,
                    ref motor,
                    transform.ValueRO.Position,
                    center,
                    id.ValueRO.Value,
                    dt);
                idleRef.ValueRW = idle;
                locomotion.ValueRW = motor;
            }
        }

        static void TickPlaza(
            ref AgentPlazaIdle idle,
            ref AgentLocomotion motor,
            float3 position,
            float3 center,
            int agentId,
            float dt)
        {
            if (idle.Radius < RadiusMin || idle.Radius > RadiusMax)
                idle.Radius = RadiusMin + agentId % 4 * 0.7f;

            var offset = position - center;
            offset.y = 0f;
            var fromCenter = math.length(offset);
            idle.Timer -= dt;

            if (fromCenter > FarDistance)
            {
                idle.Walking = 1;
                idle.Angle = math.atan2(offset.z, offset.x);
                motor.Target = RingPoint(center, idle.Angle, idle.Radius);
                motor.Moving = 1;
                return;
            }

            if (idle.Timer <= 0f)
            {
                idle.Walking = idle.Walking == 0 ? (byte)1 : (byte)0;
                var salt = (uint)agentId * 2654435761u ^ ((uint)idle.Walking + 1u) * 97u ^ math.asuint(idle.Angle);
                var roll = Random.CreateFromIndex(salt == 0 ? 1u : salt);
                idle.Timer = idle.Walking != 0 ? roll.NextFloat(3f, 6.5f) : roll.NextFloat(1.8f, 4.2f);
                if (idle.Walking != 0)
                    idle.Radius = roll.NextFloat(RadiusMin, RadiusMax);
            }

            if (idle.Walking == 0)
            {
                motor.Moving = 0;
                return;
            }

            idle.Angle += WalkAngularSpeed * dt;
            motor.Target = RingPoint(center, idle.Angle, idle.Radius);
            motor.Moving = 1;
        }

        static float3 RingPoint(float3 center, float angle, float radius)
        {
            return new float3(
                center.x + math.cos(angle) * radius,
                center.y,
                center.z + math.sin(angle) * radius);
        }
    }
}
