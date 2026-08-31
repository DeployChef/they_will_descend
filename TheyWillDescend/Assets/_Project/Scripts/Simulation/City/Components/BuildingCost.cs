using TheyWillDescend.Simulation.Economy;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Place cost on a house stamp. No TypeId — the buffer lives on the prefab entity.
    /// </summary>
    public struct BuildingCost : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float Amount;
    }

    public static class BuildingCosts
    {
        public static bool HasCost(DynamicBuffer<BuildingCost> costs)
        {
            for (var i = 0; i < costs.Length; i++)
            {
                if (costs[i].Amount > 0.0001f)
                    return true;
            }

            return false;
        }

        public static bool CanAfford(
            DynamicBuffer<BuildingCost> costs,
            DynamicBuffer<ResourceAmount> stock)
        {
            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i];
                if (cost.Amount <= 0.0001f)
                    continue;
                if (!ResourceLedger.Has(stock, cost.ResourceId, cost.Amount))
                    return false;
            }

            return true;
        }
    }
}
