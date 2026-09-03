using TheyWillDescend.Simulation.Economy;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    [InternalBufferCapacity(0)]
    public struct TechCatalogCost : IBufferElementData
    {
        public FixedString64Bytes TechId;
        public FixedString64Bytes ResourceId;
        public float Amount;
    }

    public static class TechCosts
    {
        public static bool CanAfford(
            DynamicBuffer<TechCatalogCost> costs,
            in FixedString64Bytes techId,
            DynamicBuffer<ResourceAmount> stock)
        {
            if (!costs.IsCreated || techId.IsEmpty)
                return true;
            for (var i = 0; i < costs.Length; i++)
            {
                var row = costs[i];
                if (row.TechId != techId || row.Amount <= 0.0001f)
                    continue;
                if (!ResourceLedger.Has(stock, row.ResourceId, row.Amount))
                    return false;
            }

            return true;
        }

        public static void Pay(
            DynamicBuffer<TechCatalogCost> costs,
            in FixedString64Bytes techId,
            DynamicBuffer<ResourceAmount> stock)
        {
            if (!costs.IsCreated || techId.IsEmpty)
                return;
            for (var i = 0; i < costs.Length; i++)
            {
                var row = costs[i];
                if (row.TechId != techId || row.Amount <= 0.0001f)
                    continue;
                ResourceLedger.Add(stock, row.ResourceId, -row.Amount);
            }
        }
    }
}
