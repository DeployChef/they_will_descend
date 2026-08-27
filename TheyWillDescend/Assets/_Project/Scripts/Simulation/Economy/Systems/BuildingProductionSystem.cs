using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Burst;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Economy
{
    /// <summary>
    /// Production tick: working buildings run their catalog recipe (consume / produce per game hour).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TheyWillDescend.Simulation.Agents.SyncWorkplaceLoadSystem))]
    public partial struct BuildingProductionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<ResourceAmount>();
            state.RequireForUpdate<BuildingRecipeLine>();
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

            var time = SystemAPI.GetSingleton<GameTime>();
            if (!time.IsWorkShift)
                return;
            var dayDuration = time.DayDuration;
            var stock = SystemAPI.GetSingletonBuffer<ResourceAmount>();
            var recipes = SystemAPI.GetSingletonBuffer<BuildingRecipeLine>(true);

            foreach (var (workplace, type) in
                     SystemAPI.Query<RefRO<Workplace>, RefRO<BuildingType>>()
                         .WithAll<Building>()
                         .WithNone<Construction, Headquarters>())
            {
                if (workplace.ValueRO.IsPaused || workplace.ValueRO.WorkingCount <= 0)
                    continue;
                var slots = type.ValueRO.WorkplaceSlots;
                var load = Workplace.Load01(workplace.ValueRO.WorkingCount, slots);
                if (load <= 0f)
                    continue;
                var typeId = type.ValueRO.TypeId;
                if (!BuildingRecipes.HasLines(recipes, typeId))
                    continue;
                BuildingRecipes.Apply(recipes, stock, typeId, dt, dayDuration, load);
            }
        }
    }
}
