using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    public struct AssignWorkerCommand : IBufferElementData
    {
        public int BuildingId;
        /// <summary>0 = first idle worker (HUD +). Load passes a specific AgentId.</summary>
        public int AgentId;
        /// <summary>How many idle workers to send. 0 or 1 = one.</summary>
        public int Count;
    }

    public struct UnassignWorkerCommand : IBufferElementData
    {
        public int BuildingId;
        /// <summary>How many to pull off. 0 or 1 = one. Large value = all.</summary>
        public int Count;
    }

    public struct SetWorkplacePausedCommand : IBufferElementData
    {
        public int BuildingId;
        public byte Paused;
    }
}
