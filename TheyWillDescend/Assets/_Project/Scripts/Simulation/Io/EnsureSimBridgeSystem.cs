using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct EnsureSimBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            SimBridgeAccess.GetOrCreate(state.EntityManager);
            SimBridgeAccess.GetOrCreateCityGrid(state.EntityManager);
        }
    }
}
