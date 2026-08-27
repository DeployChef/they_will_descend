using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    public struct AssignWorkerCommand : IBufferElementData
    {
        public int BuildingId;
        /// <summary>0 = first idle worker (HUD +). Load passes a specific AgentId.</summary>
        public int AgentId;
    }

    public struct UnassignWorkerCommand : IBufferElementData
    {
        public int BuildingId;
    }
}
