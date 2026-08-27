using TheyWillDescend.Simulation.Agents;
using Unity.Entities;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Bake also writes Center. This catches Play if the baking system missed LocalTransform.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AdvancePlazaIdleSystem))]
    public partial struct SyncCityCenterFromHeadquartersSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CityGrid>();
            state.RequireForUpdate<Headquarters>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var transform in
                     SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Headquarters>())
            {
                var grid = SystemAPI.GetSingletonRW<CityGrid>();
                grid.ValueRW.Center = transform.ValueRO.Position;
                return;
            }
        }
    }
}
