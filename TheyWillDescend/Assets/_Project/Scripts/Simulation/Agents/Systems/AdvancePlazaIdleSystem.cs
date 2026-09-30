using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Unassigned, assigned off-shift, or not claimed for construction: stroll the ring road
    /// either way, then stop and look at the pyramid before turning back.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AdvanceAgentCommuteSystem))]
    public partial struct AdvancePlazaIdleSystem : ISystem
    {
        const float DefaultWalkSpeed = 2f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<CityGrid>();
            state.RequireForUpdate<AgentPlazaIdle>();
            state.RequireForUpdate<GameTime>();
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

            var grid = SystemAPI.GetSingleton<CityGrid>();
            var center = grid.Center;
            var config = grid.Config;
            var onShift = SystemAPI.GetSingleton<GameTime>().IsWorkShift;

            foreach (var (idleRef, assignment, locomotion, transform, id) in
                     SystemAPI.Query<RefRW<AgentPlazaIdle>, RefRO<AgentAssignment>, RefRW<AgentLocomotion>,
                         RefRW<LocalTransform>, RefRO<AgentId>>())
            {
                if (assignment.ValueRO.HasConstructionTask)
                    continue;
                if (assignment.ValueRO.WorkplaceBuildingId != 0 && onShift)
                    continue;

                var idle = idleRef.ValueRO;
                var motor = locomotion.ValueRO;
                var pose = transform.ValueRO;
                TickPlaza(
                    ref idle,
                    ref motor,
                    ref pose,
                    center,
                    config,
                    id.ValueRO.Value,
                    dt);
                idleRef.ValueRW = idle;
                locomotion.ValueRW = motor;
                transform.ValueRW = pose;
            }
        }

        static void TickPlaza(
            ref AgentPlazaIdle idle,
            ref AgentLocomotion motor,
            ref LocalTransform pose,
            float3 center,
            in RadialGridConfig config,
            int agentId,
            float dt)
        {
            if (idle.Direction == 0)
                idle.Direction = PlazaRing.PickDirection(agentId);

            var min = PlazaRing.Min(config);
            var max = PlazaRing.Max(config);
            if (idle.Radius < min || idle.Radius > max)
                idle.Radius = PlazaRing.Lane(config, agentId);

            var offset = pose.Position - center;
            offset.y = 0f;
            var fromCenter = math.length(offset);
            idle.Timer -= dt;

            var insidePyramid = fromCenter < min - 0.35f;
            if (fromCenter > PlazaRing.Far(config) || insidePyramid)
            {
                idle.Walking = 1;
                idle.Angle = math.atan2(offset.z, offset.x);
                motor.Target = RingPoint(center, idle.Angle, idle.Radius);
                motor.Moving = 1;
                return;
            }

            if (idle.Timer <= 0f)
                ChooseNext(ref idle, config, agentId, offset);

            if (idle.Walking == 0)
            {
                motor.Moving = 0;
                Face(ref pose, -offset, dt);
                return;
            }

            var speed = motor.Speed > 0.001f ? motor.Speed : DefaultWalkSpeed;
            var omega = speed / math.max(idle.Radius, 0.5f);
            var lead = 0.35f + (agentId % 5) * 0.06f;
            idle.Angle += idle.Direction * omega * dt;
            motor.Target = RingPoint(center, idle.Angle + idle.Direction * lead, idle.Radius);
            motor.Moving = 1;
        }

        static void ChooseNext(ref AgentPlazaIdle idle, in RadialGridConfig config, int agentId, float3 offset)
        {
            var salt = (uint)agentId * 2654435761u
                       ^ ((uint)idle.Walking + 1u) * 97u
                       ^ (uint)(idle.Direction + 2) * 131u
                       ^ math.asuint(idle.Angle + idle.Timer);
            var roll = Random.CreateFromIndex(salt == 0 ? 1u : salt);

            if (idle.Walking != 0)
            {
                idle.Walking = 0;
                idle.Angle = math.atan2(offset.z, offset.x);
                idle.Timer = roll.NextFloat(1.4f, 3.8f);
                return;
            }

            if (roll.NextFloat() < 0.72f)
                idle.Direction = (sbyte)-idle.Direction;
            if (roll.NextFloat() < 0.4f)
                idle.Radius = PlazaRing.Lane(config, roll.NextInt());

            idle.Walking = 1;
            idle.Angle = math.atan2(offset.z, offset.x);
            idle.Timer = roll.NextFloat(4f, 9f);
        }

        static void Face(ref LocalTransform pose, float3 forward, float dt)
        {
            forward.y = 0f;
            if (math.lengthsq(forward) < 1e-6f)
                return;
            var desired = quaternion.LookRotationSafe(forward, math.up());
            var blend = 1f - math.exp(-5.5f * dt);
            pose.Rotation = math.slerp(pose.Rotation, desired, blend);
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
