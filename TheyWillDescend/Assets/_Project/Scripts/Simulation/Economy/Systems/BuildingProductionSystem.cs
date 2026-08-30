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
        EntityQuery _session;

        public void OnCreate(ref SystemState state)
        {
            _session = state.GetEntityQuery(
                ComponentType.ReadOnly<SimControl>(),
                ComponentType.ReadOnly<GameTime>(),
                ComponentType.ReadWrite<ResourceAmount>(),
                ComponentType.ReadOnly<ResourceInfo>(),
                ComponentType.ReadOnly<BuildingRecipeLine>());
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
            var recipes = _session.GetSingletonBuffer<BuildingRecipeLine>(true);

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
                BuildingRecipes.Apply(recipes, stock, info, typeId, dt, dayDuration, load);
            }
        }
    }
}
