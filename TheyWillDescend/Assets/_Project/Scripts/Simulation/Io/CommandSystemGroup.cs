using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// Player/load commands before movement and economy.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial class CommandSystemGroup : ComponentSystemGroup
    {
    }
}
