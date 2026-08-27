using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Same-frame apply for load. Tick order lives on the systems in CommandSystemGroup.
    /// </summary>
    public static class SimCommandPlayback
    {
        public static void Run(EntityManager em)
        {
            ConsumeSimClockCommandsSystem.Run(em);
            ConsumeDespawnAgentsSystem.Run(em);
            ConsumeDespawnBuildingsSystem.Run(em);
            ConsumeSpawnAgentCommandsSystem.Run(em);
            ConsumePlaceBuildingCommandsSystem.Run(em);
            ConsumeAssignWorkerCommandsSystem.Run(em);
            ConsumeUnassignWorkerCommandsSystem.Run(em);
        }
    }
}
