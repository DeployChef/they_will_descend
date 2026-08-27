using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// 0 = idle at plaza. Non-zero = staffed to that house (commute on shift, plaza off shift).
    /// </summary>
    public struct AgentAssignment : IComponentData
    {
        public int WorkplaceBuildingId;
        public byte Arrived;
    }
}
