using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Economy
{
    /// <summary>
    /// Run ledger row. Catalog bake creates one per known resource at amount 0.
    /// Scenario / production / place write Amount.
    /// </summary>
    public struct ResourceAmount : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float Amount;
    }

    /// <summary>
    /// Baked catalog names for HUD. Icons stay on the catalog asset.
    /// </summary>
    public struct ResourceInfo : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public FixedString64Bytes DisplayName;
    }
}
