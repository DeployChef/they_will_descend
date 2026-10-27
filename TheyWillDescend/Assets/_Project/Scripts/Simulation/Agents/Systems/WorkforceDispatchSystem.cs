using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Reconciles assignment to <see cref="Workplace.DesiredWorkers"/>.
    /// Never writes desire. Pause does not release crew. Order is Building.Id.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AdvanceAgentCommuteSystem))]
    public partial struct WorkforceDispatchSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<WorkforceSummary>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var control = SystemAPI.GetSingleton<SimControl>();
            if (control.Mode == SimRunMode.Off)
                return;

            var workplaces = SystemAPI.GetComponentLookup<Workplace>(false);
            var idle = new NativeList<AgentRef>(64, Allocator.Temp);
            var assigned = new NativeList<AgentRef>(128, Allocator.Temp);
            var houses = new NativeList<HouseRef>(32, Allocator.Temp);

            var totalWorkers = 0;
            foreach (var (assignment, type, id, entity) in
                     SystemAPI.Query<RefRW<AgentAssignment>, RefRO<AgentType>, RefRO<AgentId>>()
                         .WithEntityAccess())
            {
                if (type.ValueRO.Kind != AgentKind.Worker)
                    continue;

                totalWorkers++;
                var job = assignment.ValueRO;
                var house = job.Workplace;
                if (house != Entity.Null && !workplaces.HasComponent(house))
                {
                    job.Workplace = Entity.Null;
                    job.Arrived = 0;
                    assignment.ValueRW = job;
                    house = Entity.Null;
                }

                var row = new AgentRef
                {
                    Agent = entity,
                    House = house,
                    AgentId = id.ValueRO.Value
                };
                if (house == Entity.Null)
                    idle.Add(row);
                else
                    assigned.Add(row);
            }

            foreach (var (building, type, workplace, house) in
                     SystemAPI.Query<RefRO<Building>, RefRO<BuildingType>, RefRO<Workplace>>()
                         .WithNone<Construction>()
                         .WithEntityAccess())
            {
                houses.Add(new HouseRef
                {
                    Entity = house,
                    Id = building.ValueRO.Id,
                    Slots = type.ValueRO.WorkplaceSlots,
                    Desired = workplace.ValueRO.DesiredWorkers
                });
            }

            SortHouses(houses);
            SortAgents(idle);

            var desiredTotal = 0;
            var unmetDesired = 0;
            var crew = new NativeList<AgentRef>(16, Allocator.Temp);
            for (var h = 0; h < houses.Length; h++)
            {
                var house = houses[h];
                var slots = math.max(0, house.Slots);
                var desired = math.max(0, house.Desired);
                desiredTotal += desired;
                var target = desired < slots ? desired : slots;

                CollectCrew(assigned, house.Entity, crew);
                SortAgents(crew);

                var fact = crew.Length;
                if (fact > target)
                {
                    for (var i = crew.Length - 1; i >= target; i--)
                    {
                        var agent = crew[i].Agent;
                        var job = SystemAPI.GetComponent<AgentAssignment>(agent);
                        job.Workplace = Entity.Null;
                        job.Arrived = 0;
                        SystemAPI.SetComponent(agent, job);
                        for (var a = assigned.Length - 1; a >= 0; a--)
                        {
                            if (assigned[a].Agent != agent)
                                continue;
                            var row = assigned[a];
                            row.House = Entity.Null;
                            idle.Add(row);
                            assigned.RemoveAt(a);
                            break;
                        }

                        crew.RemoveAt(i);
                    }

                    SortAgents(idle);
                    fact = crew.Length;
                }
                else if (fact < target)
                {
                    var needed = target - fact;
                    var take = 0;
                    while (take < needed && idle.Length > 0)
                    {
                        var agent = idle[0];
                        idle.RemoveAt(0);
                        var job = SystemAPI.GetComponent<AgentAssignment>(agent.Agent);
                        job.Workplace = house.Entity;
                        job.Arrived = 0;
                        SystemAPI.SetComponent(agent.Agent, job);
                        agent.House = house.Entity;
                        assigned.Add(agent);
                        take++;
                    }

                    fact += take;
                }

                workplaces.GetRefRW(house.Entity).ValueRW.AssignedCount = fact;
                unmetDesired += math.max(0, target - fact);
            }

            var summary = SystemAPI.GetSingletonRW<WorkforceSummary>();
            summary.ValueRW = new WorkforceSummary
            {
                TotalWorkers = totalWorkers,
                IdleWorkers = idle.Length,
                DesiredTotal = desiredTotal,
                UnmetDesired = unmetDesired
            };

            crew.Dispose();
            idle.Dispose();
            assigned.Dispose();
            houses.Dispose();
        }

        static void CollectCrew(NativeList<AgentRef> assigned, Entity house, NativeList<AgentRef> crew)
        {
            crew.Clear();
            for (var i = 0; i < assigned.Length; i++)
            {
                if (assigned[i].House == house)
                    crew.Add(assigned[i]);
            }
        }

        static void SortHouses(NativeList<HouseRef> houses)
        {
            for (var i = 1; i < houses.Length; i++)
            {
                var current = houses[i];
                var j = i - 1;
                while (j >= 0 && houses[j].Id > current.Id)
                {
                    houses[j + 1] = houses[j];
                    j--;
                }

                houses[j + 1] = current;
            }
        }

        static void SortAgents(NativeList<AgentRef> agents)
        {
            for (var i = 1; i < agents.Length; i++)
            {
                var current = agents[i];
                var j = i - 1;
                while (j >= 0 && agents[j].AgentId > current.AgentId)
                {
                    agents[j + 1] = agents[j];
                    j--;
                }

                agents[j + 1] = current;
            }
        }

        struct HouseRef
        {
            public Entity Entity;
            public int Id;
            public int Slots;
            public int Desired;
        }

        struct AgentRef
        {
            public Entity Agent;
            public Entity House;
            public int AgentId;
        }
    }
}
