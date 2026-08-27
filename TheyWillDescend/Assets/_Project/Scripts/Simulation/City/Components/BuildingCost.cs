using TheyWillDescend.Simulation.Economy;
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

    public static class BuildingCosts
    {
        public static bool HasCost(DynamicBuffer<BuildingCost> costs, in FixedString64Bytes typeId)
        {
            for (var i = 0; i < costs.Length; i++)
            {
                if (costs[i].TypeId == typeId && costs[i].Amount > 0.0001f)
                    return true;
            }

            return false;
        }

        public static bool CanAfford(
            DynamicBuffer<BuildingCost> costs,
            DynamicBuffer<ResourceAmount> stock,
            in FixedString64Bytes typeId)
        {
            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i];
                if (cost.TypeId != typeId || cost.Amount <= 0.0001f)
                    continue;
                if (!ResourceLedger.Has(stock, cost.ResourceId, cost.Amount))
                    return false;
            }

            return true;
        }
    }
}
