using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Temporary commute: pick a finished house as <see cref="AgentLocomotion.Target"/>.
    /// Hunt/scout will be other behaviors writing the same motor.
    /// </summary>
    public struct AgentHousePatrol : IComponentData
    {
    }
}
