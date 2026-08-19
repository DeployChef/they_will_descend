using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// Same-frame apply for load. Tick order lives on the systems in CommandSystemGroup.
    /// </summary>
    public static class SimCommandPlayback
    {
        public static void Run(EntityManager em)
        {
            ConsumeDespawnAgentsSystem.Run(em);
            ConsumeDespawnBuildingsSystem.Run(em);
            ConsumeSpawnAgentCommandsSystem.Run(em);
            ConsumePlaceBuildingCommandsSystem.Run(em);
        }
    }
}
