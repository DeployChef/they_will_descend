using TheyWillDescend.Simulation.Economy;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Place cost row. On the session catalog this is <see cref="BuildingCatalogCost"/>
    /// (keyed by type). Instance houses do not keep costs.
    /// </summary>
    public struct BuildingCost : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float Amount;
    }

    /// <summary>
    /// Place cost for one catalog type. Lives on the session, not on a baked prefab.
    /// </summary>
    public struct BuildingCatalogCost : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public FixedString64Bytes ResourceId;
        public float Amount;
    }

    /// <summary>
    /// Immutable prefab-default cost row. Run setup rebuilds the resolved
    /// <see cref="BuildingCatalogCost"/> buffer from these rows.
    /// </summary>
    public struct BaseBuildingCatalogCost : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public FixedString64Bytes ResourceId;
        public float Amount;

        public BuildingCatalogCost ToResolved() => new()
        {
            TypeId = TypeId,
            ResourceId = ResourceId,
            Amount = Amount
        };
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

        public static bool HasCost(
            DynamicBuffer<BuildingCatalogCost> catalog,
            in FixedString64Bytes typeId)
        {
            if (!catalog.IsCreated || typeId.IsEmpty)
                return false;
            for (var i = 0; i < catalog.Length; i++)
            {
                var row = catalog[i];
                if (row.TypeId == typeId && row.Amount > 0.0001f)
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

        public static bool CanAfford(
            DynamicBuffer<BuildingCatalogCost> catalog,
            in FixedString64Bytes typeId,
            DynamicBuffer<ResourceAmount> stock)
        {
            if (!catalog.IsCreated || typeId.IsEmpty)
                return true;
            for (var i = 0; i < catalog.Length; i++)
            {
                var row = catalog[i];
                if (row.TypeId != typeId || row.Amount <= 0.0001f)
                    continue;
                if (!ResourceLedger.Has(stock, row.ResourceId, row.Amount))
                    return false;
            }

            return true;
        }

        public static void Pay(
            DynamicBuffer<BuildingCatalogCost> catalog,
            in FixedString64Bytes typeId,
            DynamicBuffer<ResourceAmount> stock)
        {
            if (!catalog.IsCreated || typeId.IsEmpty)
                return;
            for (var i = 0; i < catalog.Length; i++)
            {
                var row = catalog[i];
                if (row.TypeId != typeId || row.Amount <= 0.0001f)
                    continue;
                ResourceLedger.Add(stock, row.ResourceId, -row.Amount);
            }
        }
    }
}
