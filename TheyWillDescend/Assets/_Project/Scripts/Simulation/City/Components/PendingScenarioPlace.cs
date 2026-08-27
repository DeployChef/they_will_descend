using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Baked scenario houses. Play turns these into PlaceBuildingCommand.
    /// Instantiating catalog prefabs during bake duplicates EntityGuid in Live Conversion.
    /// </summary>
    public struct PendingScenarioPlace : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public int Cluster;
        public int Radial;
    }
}
