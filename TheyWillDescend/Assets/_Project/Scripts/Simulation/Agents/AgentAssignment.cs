using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// 0 = idle at plaza. Non-zero = commute / work that building.
    /// </summary>
    public struct AgentAssignment : IComponentData
    {
        public int WorkplaceBuildingId;
        public byte Arrived;
    }
}
