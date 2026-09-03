using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Workforce reconciliation loop (Desired State).
    /// Dynamically dispatches idle plaza workers to buildings with unmet DesiredWorkers,
    /// and releases workers when a building is paused, dismantled, or has its DesiredWorkers reduced.
    /// Self-heals when agents die or spawn without requiring transactional commands or rollbacks.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AdvanceAgentCommuteSystem))]
    public partial struct WorkforceDispatchSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
            state.RequireForUpdate<Workplace>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)

        {
            var assignedCounts = new NativeHashMap<int, int>(32, Allocator.Temp);
            var idleAgents = new NativeList<Entity>(64, Allocator.Temp);
            var assignedAgents = new NativeList<AgentRef>(128, Allocator.Temp);

            foreach (var (assignment, type, entity) in
                     SystemAPI.Query<RefRO<AgentAssignment>, RefRO<AgentType>>()
                         .WithEntityAccess())
            {
                if (type.ValueRO.Kind != AgentKind.Worker)
                    continue;

                var bId = assignment.ValueRO.WorkplaceBuildingId;
                if (bId == 0)
                {
                    idleAgents.Add(entity);
                }
                else
                {
                    if (assignedCounts.TryGetValue(bId, out var current))
                        assignedCounts[bId] = current + 1;
                    else
                        assignedCounts.Add(bId, 1);

                    assignedAgents.Add(new AgentRef { Entity = entity, BuildingId = bId });
                }
            }

            var idleIndex = 0;
            foreach (var (building, type, workplace, buildingEntity) in
                     SystemAPI.Query<RefRO<Building>, RefRO<BuildingType>, RefRW<Workplace>>()
                         .WithNone<Construction>()
                         .WithEntityAccess())
            {
                var bId = building.ValueRO.Id;
                var maxSlots = type.ValueRO.WorkplaceSlots;
                var desired = workplace.ValueRO.IsPaused ? 0 : math.clamp(workplace.ValueRO.DesiredWorkers, 0, maxSlots);
                var current = assignedCounts.TryGetValue(bId, out var c) ? c : 0;

                if (current < desired)
                {
                    var needed = desired - current;
                    var assigned = 0;
                    while (needed > 0 && idleIndex < idleAgents.Length)
                    {
                        var agentEntity = idleAgents[idleIndex++];
                        var assign = SystemAPI.GetComponent<AgentAssignment>(agentEntity);
                        assign.WorkplaceBuildingId = bId;
                        assign.Arrived = 0;
                        SystemAPI.SetComponent(agentEntity, assign);
                        needed--;
                        assigned++;
                    }

                    if (needed > 0)
                    {
                        // No free workers available to fulfill remainder: clamp DesiredWorkers to actual assigned
                        workplace.ValueRW.DesiredWorkers = current + assigned;
                    }
                }

                else if (current > desired)
                {
                    var excess = current - desired;
                    for (var i = assignedAgents.Length - 1; i >= 0 && excess > 0; i--)
                    {
                        if (assignedAgents[i].BuildingId != bId)
                            continue;

                        var agentEntity = assignedAgents[i].Entity;
                        var assign = SystemAPI.GetComponent<AgentAssignment>(agentEntity);
                        assign.WorkplaceBuildingId = 0;
                        assign.Arrived = 0;
                        SystemAPI.SetComponent(agentEntity, assign);
                        excess--;
                    }
                }
            }

            assignedCounts.Dispose();
            idleAgents.Dispose();
            assignedAgents.Dispose();
        }

        struct AgentRef
        {
            public Entity Entity;
            public int BuildingId;
        }
    }
}
