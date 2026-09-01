using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Starting houses copied from the scenario SO at bake. Play spawns them through
    /// the same <c>SpawnHouse</c> path as Place (InstantComplete, no pay).
    /// </summary>
    public struct PendingScenarioPlace : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public int Cluster;
        public int Radial;
    }
}
