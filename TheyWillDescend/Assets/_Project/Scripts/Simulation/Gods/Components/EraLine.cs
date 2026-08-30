using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Gods
{
    /// <summary>
    /// Catalog row. Buffer order is era index.
    /// </summary>
    public struct EraLine : IBufferElementData
    {
        public FixedString64Bytes EraId;
        public FixedString64Bytes DisplayName;
        public int DurationDays;
        public float MaxLoyalty;
        public float TributeEnergyMul;
        public float LoyaltyPerEnergy;
    }

    public struct EraTributeLine : IBufferElementData
    {
        public int EraIndex;
        public FixedString64Bytes ResourceId;
    }
}
