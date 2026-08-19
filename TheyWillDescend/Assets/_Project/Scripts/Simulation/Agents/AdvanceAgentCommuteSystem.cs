using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Assigned workers walk to the house; arrived → stand and mark Workplace.Working.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Io.CommandSystemGroup))]
    [UpdateAfter(typeof(AdvanceConstructionSystem))]
    public partial struct AdvanceAgentCommuteSystem : ISystem
    {
        const float ArriveDistance = 1.1f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<AgentAssignment>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.GetSingleton<SimControl>().DeltaTime;
            if (dt <= 0f)
                return;

            var houses = new NativeList<HouseRef>(8, state.WorldUpdateAllocator);
            foreach (var (building, transform, entity) in
                     SystemAPI.Query<RefRO<Building>, RefRO<LocalTransform>>()
                         .WithAll<Workplace>()
                         .WithNone<Construction, Headquarters>()
                         .WithEntityAccess())
            {
                houses.Add(new HouseRef
                {
                    Id = building.ValueRO.Id,
                    Position = transform.ValueRO.Position,
                    Entity = entity
                });
            }

            foreach (var (assignment, locomotion, transform) in
                     SystemAPI.Query<RefRW<AgentAssignment>, RefRW<AgentLocomotion>, RefRO<LocalTransform>>()
                         .WithAll<AgentId>())
            {
                var job = assignment.ValueRO;
                if (job.WorkplaceBuildingId == 0)
                    continue;

                var houseIndex = -1;
                for (var i = 0; i < houses.Length; i++)
                {
                    if (houses[i].Id != job.WorkplaceBuildingId)
                        continue;
                    houseIndex = i;
                    break;
                }

                var motor = locomotion.ValueRO;
                if (houseIndex < 0)
                {
                    job.WorkplaceBuildingId = 0;
                    job.Arrived = 0;
                    motor.Moving = 0;
                    assignment.ValueRW = job;
                    locomotion.ValueRW = motor;
                    continue;
                }

                var house = houses[houseIndex];
                var delta = house.Position - transform.ValueRO.Position;
                delta.y = 0f;
                var arrived = math.length(delta) <= ArriveDistance;
                job.Arrived = arrived ? (byte)1 : (byte)0;
                motor.Target = house.Position;
                motor.Moving = arrived ? (byte)0 : (byte)1;
                assignment.ValueRW = job;
                locomotion.ValueRW = motor;

                if (!SystemAPI.HasComponent<Workplace>(house.Entity))
                    continue;

                var workplace = SystemAPI.GetComponentRW<Workplace>(house.Entity);
                workplace.ValueRW.Working = arrived ? (byte)1 : (byte)0;
            }
        }

        struct HouseRef
        {
            public int Id;
            public float3 Position;
            public Entity Entity;
        }
    }
}
