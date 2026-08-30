using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Gods
{
    /// <summary>
    /// Catalog row. Buffer order is era index.
    /// Numeric fields stay before <see cref="Summary"/> so a stale bake cannot zero MaxLoyalty.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct EraLine : IBufferElementData
    {
        public FixedString64Bytes EraId;
        public FixedString64Bytes DisplayName;
        public int DurationDays;
        public float MaxLoyalty;
        public float TributeEnergyMul;
        public float LoyaltyPerEnergy;
        public FixedString512Bytes Summary;
    }

    public struct EraTributeLine : IBufferElementData
    {
        public int EraIndex;
        public FixedString64Bytes ResourceId;
    }
}
