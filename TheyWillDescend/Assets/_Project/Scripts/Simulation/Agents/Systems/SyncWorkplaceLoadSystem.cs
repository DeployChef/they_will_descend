using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Recounts WorkingCount after commute. AssignedCount belongs to the dispatcher.
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
                         .WithNone<Construction>())
            {
                workplace.ValueRW.WorkingCount = 0;
            }

            var workplaces = SystemAPI.GetComponentLookup<Workplace>(false);
            var constructions = SystemAPI.GetComponentLookup<Construction>(true);

            foreach (var assignment in SystemAPI.Query<RefRO<AgentAssignment>>())
            {
                var job = assignment.ValueRO;
                var house = job.Workplace;
                if (house == Entity.Null)
                    continue;
                if (!workplaces.HasComponent(house) || constructions.HasComponent(house))
                    continue;
                if (job.Arrived == 0 || job.HasConstructionTask)
                    continue;
                var workplace = workplaces.GetRefRW(house);
                workplace.ValueRW.WorkingCount++;
            }
        }
    }
}
