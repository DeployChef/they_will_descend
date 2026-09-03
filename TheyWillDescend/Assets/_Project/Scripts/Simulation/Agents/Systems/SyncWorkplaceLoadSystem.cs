using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Recounts AssignedCount / WorkingCount from agents. Assignment is the write model.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AdvanceConstructionSystem))]
    public partial struct SyncWorkplaceLoadSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<Workplace>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var workplace in SystemAPI.Query<RefRW<Workplace>>()
                         .WithAll<Building>()
                         .WithNone<Construction, Headquarters>())
            {
                workplace.ValueRW.AssignedCount = 0;
                workplace.ValueRW.WorkingCount = 0;
            }

            var houses = new NativeHashMap<int, Entity>(16, state.WorldUpdateAllocator);
            foreach (var (building, entity) in
                     SystemAPI.Query<RefRO<Building>>()
                         .WithAll<Workplace>()
                         .WithNone<Construction, Headquarters>()
                         .WithEntityAccess())
            {
                houses.TryAdd(building.ValueRO.Id, entity);
            }

            foreach (var assignment in SystemAPI.Query<RefRO<AgentAssignment>>())
            {
                var job = assignment.ValueRO;
                if (job.WorkplaceBuildingId == 0 || job.HasConstructionTask)
                    continue;
                if (!houses.TryGetValue(job.WorkplaceBuildingId, out var house))
                    continue;
                var workplace = SystemAPI.GetComponentRW<Workplace>(house);
                workplace.ValueRW.AssignedCount++;
                if (job.Arrived != 0)
                    workplace.ValueRW.WorkingCount++;
            }
        }
    }
}
