using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// One slot. WorkerAgentId 0 = empty. Working 1 = worker arrived.
    /// </summary>
    public struct Workplace : IComponentData
    {
        public int WorkerAgentId;
        public byte Working;
    }
}
