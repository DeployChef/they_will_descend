using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Session read model for HUD. Written only by <see cref="WorkforceDispatchSystem"/>.
    /// </summary>
    public struct WorkforceSummary : IComponentData
    {
        public int TotalWorkers;
        public int IdleWorkers;
        public int DesiredTotal;
        public int UnmetDesired;
    }
}
