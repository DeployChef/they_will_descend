using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Economy
{
    /// <summary>
    /// Finished house with a worker on-site ticks Resource 1.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TheyWillDescend.Simulation.Agents.AdvanceAgentCommuteSystem))]
    public partial struct ProduceResourceSystem : ISystem
    {
        const float Resource1PerSecond = 1f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<ResourceStock>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<SimControl>())
                return;
            var dt = SystemAPI.GetSingleton<SimControl>().DeltaTime;
            if (dt <= 0f)
                return;

            var produced = 0f;
            foreach (var workplace in
                     SystemAPI.Query<RefRO<Workplace>>()
                         .WithAll<Building>()
                         .WithNone<Construction, Headquarters>())
            {
                if (workplace.ValueRO.Working != 0 && workplace.ValueRO.WorkerAgentId != 0)
                    produced += Resource1PerSecond * dt;
            }

            if (produced <= 0f || !SystemAPI.HasSingleton<ResourceStock>())
                return;

            var stock = SystemAPI.GetSingletonRW<ResourceStock>();
            stock.ValueRW.Resource1 += produced;
        }
    }
}
