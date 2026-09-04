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
    /// Night (and idle by day): nearest free workers claim a construction site.
    /// Does not touch <see cref="AgentAssignment.WorkplaceBuildingId"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(AdvanceGameTimeSystem))]
    [UpdateBefore(typeof(AdvanceAgentCommuteSystem))]
    public partial struct ClaimConstructionCrewSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<AgentAssignment>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var control = SystemAPI.GetSingleton<SimControl>();
            if (!control.IsRunning)
                return;

            var onShift = SystemAPI.GetSingleton<GameTime>().IsWorkShift;
            var sites = new NativeList<Site>(8, state.WorldUpdateAllocator);
            foreach (var (building, type, transform) in
                     SystemAPI.Query<RefRO<Building>, RefRO<BuildingType>, RefRO<LocalTransform>>()
                         .WithAll<Construction>())

            {
                sites.Add(new Site
                {
                    Id = building.ValueRO.Id,
                    Position = transform.ValueRO.Position,
                    Slots = ConstructionCrew.ResolveSlots(type.ValueRO.ConstructionCrewSlots)
                });
            }

            SortById(sites);

            var agents = new NativeList<AgentRef>(64, state.WorldUpdateAllocator);
            foreach (var (assignment, transform, entity) in
                     SystemAPI.Query<RefRO<AgentAssignment>, RefRO<LocalTransform>>()
                         .WithAll<AgentId>()
                         .WithEntityAccess())
            {
                agents.Add(new AgentRef
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position,
                    Job = assignment.ValueRO
                });
            }

            for (var i = 0; i < agents.Length; i++)
            {
                var agent = agents[i];
                var job = agent.Job;
                if (job.ConstructionBuildingId == 0)
                    continue;

                var siteIndex = FindSite(sites, job.ConstructionBuildingId);
                if (siteIndex < 0 || !job.CanKeepConstruction(onShift))
                {
                    job.ConstructionBuildingId = 0;
                    job.Arrived = 0;
                    agent.Job = job;
                    agents[i] = agent;
                    SystemAPI.SetComponent(agent.Entity, job);
                    continue;
                }

                var site = sites[siteIndex];
                site.Claimed++;
                sites[siteIndex] = site;
            }

            for (var s = 0; s < sites.Length; s++)
            {
                var site = sites[s];
                while (site.Claimed < site.Slots)
                {
                    var best = -1;
                    var bestDist = float.MaxValue;
                    for (var i = 0; i < agents.Length; i++)
                    {
                        var candidate = agents[i].Job;
                        if (!candidate.IsFreeForConstruction(onShift))
                            continue;
                        var dist = DistSqXz(agents[i].Position, site.Position);
                        if (dist >= bestDist)
                            continue;
                        bestDist = dist;
                        best = i;
                    }

                    if (best < 0)
                        break;

                    var agent = agents[best];
                    var job = agent.Job;
                    job.ConstructionBuildingId = site.Id;
                    job.Arrived = 0;
                    agent.Job = job;
                    agents[best] = agent;
                    SystemAPI.SetComponent(agent.Entity, job);
                    site.Claimed++;
                }

                sites[s] = site;
            }
        }

        static void SortById(NativeList<Site> sites)
        {
            for (var i = 1; i < sites.Length; i++)
            {
                var current = sites[i];
                var j = i - 1;
                while (j >= 0 && sites[j].Id > current.Id)
                {
                    sites[j + 1] = sites[j];
                    j--;
                }

                sites[j + 1] = current;
            }
        }

        static int FindSite(NativeList<Site> sites, int buildingId)
        {
            for (var i = 0; i < sites.Length; i++)
            {
                if (sites[i].Id == buildingId)
                    return i;
            }

            return -1;
        }

        static float DistSqXz(float3 a, float3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        struct Site
        {
            public int Id;
            public float3 Position;
            public int Slots;
            public int Claimed;
        }

        struct AgentRef
        {
            public Entity Entity;
            public float3 Position;
            public AgentAssignment Job;
        }
    }
}
