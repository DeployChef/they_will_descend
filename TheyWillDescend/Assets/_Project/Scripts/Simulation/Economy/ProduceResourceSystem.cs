using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Economy
{
    /// <summary>
    /// TEMPORARY: one output field on <see cref="BuildingType"/> ticks the session ledger.
    /// Replace with a recipe blob before heat / needs / multi-output.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TheyWillDescend.Simulation.Agents.AdvanceAgentCommuteSystem))]
    public partial struct ProduceResourceSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<ResourceAmount>();
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

            var stock = SystemAPI.GetSingletonBuffer<ResourceAmount>();
            foreach (var (workplace, type) in
                     SystemAPI.Query<RefRO<Workplace>, RefRO<BuildingType>>()
                         .WithAll<Building>()
                         .WithNone<Construction, Headquarters>())
            {
                if (workplace.ValueRO.Working == 0 || workplace.ValueRO.WorkerAgentId == 0)
                    continue;
                var resourceId = type.ValueRO.ProduceResourceId;
                var rate = type.ValueRO.ProducePerSecond;
                if (resourceId.IsEmpty || rate <= 0f)
                    continue;
                ResourceLedger.Add(stock, resourceId, rate * dt);
            }
        }
    }
}
