using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Burst;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Economy
{
    /// <summary>
    /// Production tick: working buildings run the recipe on their own stamp buffer.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TheyWillDescend.Simulation.Agents.SyncWorkplaceLoadSystem))]
    public partial struct BuildingProductionSystem : ISystem
    {
        EntityQuery _session;

        public void OnCreate(ref SystemState state)
        {
            _session = state.GetEntityQuery(
                ComponentType.ReadOnly<SimControl>(),
                ComponentType.ReadOnly<GameTime>(),
                ComponentType.ReadWrite<ResourceAmount>(),
                ComponentType.ReadOnly<ResourceInfo>());
            state.RequireForUpdate(_session);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var control = _session.GetSingleton<SimControl>();
            if (!control.IsRunning)
                return;
            var dt = control.DeltaTime;
            if (dt <= 0f)
                return;

            var time = _session.GetSingleton<GameTime>();
            if (!time.IsWorkShift)
                return;
            var dayDuration = time.DayDuration;
            var stock = _session.GetSingletonBuffer<ResourceAmount>();
            var info = _session.GetSingletonBuffer<ResourceInfo>(true);

            foreach (var (workplace, type, recipes) in
                     SystemAPI.Query<RefRO<Workplace>, RefRO<BuildingType>, DynamicBuffer<BuildingRecipeLine>>()
                         .WithAll<Building>()
                         .WithNone<Construction, Headquarters>())
            {
                if (workplace.ValueRO.IsPaused || workplace.ValueRO.WorkingCount <= 0)
                    continue;
                var slots = type.ValueRO.WorkplaceSlots;
                var load = Workplace.Load01(workplace.ValueRO.WorkingCount, slots);
                if (load <= 0f)
                    continue;
                if (!BuildingRecipes.HasLines(recipes))
                    continue;
                BuildingRecipes.Apply(recipes, stock, info, dt, dayDuration, load);
            }
        }
    }
}
