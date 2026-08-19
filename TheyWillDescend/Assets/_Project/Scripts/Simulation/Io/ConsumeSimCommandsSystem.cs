using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    public partial struct ConsumeSimCommandsSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            SimCommandProcessor.Run(state.EntityManager);
        }
    }
}
