using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    public partial struct ConsumeSimCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimBridge>();
            state.RequireForUpdate<SimPrototypes>();
        }

        public void OnUpdate(ref SystemState state)
        {
            SimCommandProcessor.Run(state.EntityManager);
        }
    }
}
