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
    /// Construction claim walks 24/7. Workplace commute is on-shift only.
    /// Off shift, assigned workers stay staffed; plaza takes over unless they are building.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ClaimConstructionCrewSystem))]
    public partial struct AdvanceAgentCommuteSystem : ISystem
    {
        const float ArriveDistance = 1.1f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<AgentAssignment>();
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

            var transforms = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var constructions = SystemAPI.GetComponentLookup<Construction>(true);
            var workplaces = SystemAPI.GetComponentLookup<Workplace>(true);

            foreach (var (assignment, locomotion, transform) in
                     SystemAPI.Query<RefRW<AgentAssignment>, RefRW<AgentLocomotion>, RefRO<LocalTransform>>()
                         .WithAll<AgentId>())
            {
                var job = assignment.ValueRO;
                if (!job.HasConstructionTask)
                    continue;

                var motor = locomotion.ValueRO;
                var site = job.ConstructionSite;
                if (!constructions.HasComponent(site) || !transforms.HasComponent(site)
                    || !SteerTo(transforms[site].Position, transform.ValueRO.Position, ref job, ref motor))
                {
                    job.ConstructionSite = Entity.Null;
                    job.Arrived = 0;
                    motor.Moving = 0;
                }

                assignment.ValueRW = job;
                locomotion.ValueRW = motor;
            }

            var onShift = SystemAPI.GetSingleton<GameTime>().IsWorkShift;
            if (!onShift)
            {
                foreach (var assignment in SystemAPI.Query<RefRW<AgentAssignment>>().WithAll<AgentId>())
                {
                    if (assignment.ValueRO.HasConstructionTask)
                        continue;
                    if (assignment.ValueRO.Workplace == Entity.Null || assignment.ValueRO.Arrived == 0)
                        continue;
                    assignment.ValueRW.Arrived = 0;
                }

                return;
            }

            foreach (var (assignment, locomotion, transform) in
                     SystemAPI.Query<RefRW<AgentAssignment>, RefRW<AgentLocomotion>, RefRO<LocalTransform>>()
                         .WithAll<AgentId>())
            {
                var job = assignment.ValueRO;
                if (job.HasConstructionTask)
                    continue;
                var house = job.Workplace;
                if (house == Entity.Null)
                    continue;

                var motor = locomotion.ValueRO;
                if (!workplaces.HasComponent(house) || constructions.HasComponent(house)
                    || !transforms.HasComponent(house)
                    || !SteerTo(transforms[house].Position, transform.ValueRO.Position, ref job, ref motor))
                {
                    job.Workplace = Entity.Null;
                    job.Arrived = 0;
                    motor.Moving = 0;
                }

                assignment.ValueRW = job;
                locomotion.ValueRW = motor;
            }
        }

        static bool SteerTo(
            float3 goal,
            float3 position,
            ref AgentAssignment job,
            ref AgentLocomotion motor)
        {
            var delta = goal - position;
            delta.y = 0f;
            var arrived = math.length(delta) <= ArriveDistance;
            job.Arrived = arrived ? (byte)1 : (byte)0;
            motor.Target = goal;
            motor.Moving = arrived ? (byte)0 : (byte)1;
            return true;
        }
    }
}
