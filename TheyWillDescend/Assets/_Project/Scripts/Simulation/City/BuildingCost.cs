using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Place cost for a catalog type. Several rows per TypeId.
    /// </summary>
    public struct BuildingCost : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public FixedString64Bytes ResourceId;
        public float Amount;
    }
}
