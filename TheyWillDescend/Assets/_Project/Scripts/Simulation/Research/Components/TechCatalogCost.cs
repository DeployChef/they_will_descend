using TheyWillDescend.Simulation.Economy;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// One resource cost on a tech card. Packs/difficulty may rewrite Amount later.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TechCatalogCost : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float Amount;
    }

    public static class TechCosts
    {
        public static bool CanAfford(
            DynamicBuffer<TechCatalogCost> costs,
            DynamicBuffer<ResourceAmount> stock)
        {
            if (!costs.IsCreated)
                return true;
            for (var i = 0; i < costs.Length; i++)
            {
                var row = costs[i];
                if (row.Amount <= 0.0001f)
                    continue;
                if (!ResourceLedger.Has(stock, row.ResourceId, row.Amount))
                    return false;
            }

            return true;
        }

        public static void Pay(
            DynamicBuffer<TechCatalogCost> costs,
            DynamicBuffer<ResourceAmount> stock)
        {
            if (!costs.IsCreated)
                return;
            for (var i = 0; i < costs.Length; i++)
            {
                var row = costs[i];
                if (row.Amount <= 0.0001f)
                    continue;
                ResourceLedger.Add(stock, row.ResourceId, -row.Amount);
            }
        }
    }
}
