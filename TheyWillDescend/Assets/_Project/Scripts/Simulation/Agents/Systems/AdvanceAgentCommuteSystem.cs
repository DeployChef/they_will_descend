using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Burst;
using Unity.Collections;
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

            var sites = new NativeList<HouseRef>(8, state.WorldUpdateAllocator);
            foreach (var (building, transform) in
                     SystemAPI.Query<RefRO<Building>, RefRO<LocalTransform>>()
                         .WithAll<Construction>())

            {
                sites.Add(new HouseRef
                {
                    Id = building.ValueRO.Id,
                    Position = transform.ValueRO.Position
                });
            }

            foreach (var (assignment, locomotion, transform) in
                     SystemAPI.Query<RefRW<AgentAssignment>, RefRW<AgentLocomotion>, RefRO<LocalTransform>>()
                         .WithAll<AgentId>())
            {
                var job = assignment.ValueRO;
                if (!job.HasConstructionTask)
                    continue;

                var motor = locomotion.ValueRO;
                if (!TrySteerTo(sites, job.ConstructionBuildingId, transform.ValueRO.Position, ref job, ref motor))
                {
                    job.ConstructionBuildingId = 0;
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
                    if (assignment.ValueRO.WorkplaceBuildingId == 0 || assignment.ValueRO.Arrived == 0)
                        continue;
                    assignment.ValueRW.Arrived = 0;
                }

                return;
            }

            var houses = new NativeList<HouseRef>(8, state.WorldUpdateAllocator);
            foreach (var (building, transform) in
                     SystemAPI.Query<RefRO<Building>, RefRO<LocalTransform>>()
                         .WithAll<Workplace>()
                         .WithNone<Construction>())

            {
                houses.Add(new HouseRef
                {
                    Id = building.ValueRO.Id,
                    Position = transform.ValueRO.Position
                });
            }

            foreach (var (assignment, locomotion, transform) in
                     SystemAPI.Query<RefRW<AgentAssignment>, RefRW<AgentLocomotion>, RefRO<LocalTransform>>()
                         .WithAll<AgentId>())
            {
                var job = assignment.ValueRO;
                if (job.HasConstructionTask)
                    continue;
                if (job.WorkplaceBuildingId == 0)
                    continue;

                var motor = locomotion.ValueRO;
                if (!TrySteerTo(houses, job.WorkplaceBuildingId, transform.ValueRO.Position, ref job, ref motor))
                {
                    job.WorkplaceBuildingId = 0;
                    job.Arrived = 0;
                    motor.Moving = 0;
                }

                assignment.ValueRW = job;
                locomotion.ValueRW = motor;
            }
        }

        static bool TrySteerTo(
            NativeList<HouseRef> houses,
            int buildingId,
            float3 position,
            ref AgentAssignment job,
            ref AgentLocomotion motor)
        {
            var houseIndex = -1;
            for (var i = 0; i < houses.Length; i++)
            {
                if (houses[i].Id != buildingId)
                    continue;
                houseIndex = i;
                break;
            }

            if (houseIndex < 0)
                return false;

            var house = houses[houseIndex];
            var delta = house.Position - position;
            delta.y = 0f;
            var arrived = math.length(delta) <= ArriveDistance;
            job.Arrived = arrived ? (byte)1 : (byte)0;
            motor.Target = house.Position;
            motor.Moving = arrived ? (byte)0 : (byte)1;
            return true;
        }

        struct HouseRef
        {
            public int Id;
            public float3 Position;
        }
    }
}
