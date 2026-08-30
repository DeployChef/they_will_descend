using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Economy
{
    public static class ResourceLedger
    {
        public static int IndexOf(DynamicBuffer<ResourceAmount> stock, in FixedString64Bytes resourceId)
        {
            if (resourceId.IsEmpty)
                return -1;
            for (var i = 0; i < stock.Length; i++)
            {
                if (stock[i].ResourceId == resourceId)
                    return i;
            }

            return -1;
        }

        public static int InfoIndexOf(DynamicBuffer<ResourceInfo> info, in FixedString64Bytes resourceId)
        {
            if (resourceId.IsEmpty)
                return -1;
            for (var i = 0; i < info.Length; i++)
            {
                if (info[i].ResourceId == resourceId)
                    return i;
            }

            return -1;
        }

        public static float Get(DynamicBuffer<ResourceAmount> stock, in FixedString64Bytes resourceId)
        {
            var index = IndexOf(stock, resourceId);
            return index < 0 ? 0f : stock[index].Amount;
        }

        public static float StockCap(DynamicBuffer<ResourceInfo> info, in FixedString64Bytes resourceId)
        {
            var index = InfoIndexOf(info, resourceId);
            return index < 0 ? 0f : info[index].StockCap;
        }

        public static float EnergyValue(DynamicBuffer<ResourceInfo> info, in FixedString64Bytes resourceId)
        {
            var index = InfoIndexOf(info, resourceId);
            return index < 0 ? 0f : info[index].EnergyValue;
        }

        public static bool CanFeed(DynamicBuffer<ResourceInfo> info, in FixedString64Bytes resourceId)
        {
            var index = InfoIndexOf(info, resourceId);
            return index >= 0 && info[index].CanFeed != 0;
        }

        public static void Set(DynamicBuffer<ResourceAmount> stock, in FixedString64Bytes resourceId, float amount)
        {
            var index = IndexOf(stock, resourceId);
            if (index < 0)
                return;
            var row = stock[index];
            row.Amount = amount < 0f ? 0f : amount;
            stock[index] = row;
        }

        public static void SetClamped(
            DynamicBuffer<ResourceAmount> stock,
            DynamicBuffer<ResourceInfo> info,
            in FixedString64Bytes resourceId,
            float amount)
        {
            var index = IndexOf(stock, resourceId);
            if (index < 0)
                return;
            var row = stock[index];
            row.Amount = Clamp(amount, StockCap(info, resourceId));
            stock[index] = row;
        }

        public static void Add(DynamicBuffer<ResourceAmount> stock, in FixedString64Bytes resourceId, float delta)
        {
            var index = IndexOf(stock, resourceId);
            if (index < 0 || delta == 0f)
                return;
            var row = stock[index];
            var next = row.Amount + delta;
            row.Amount = next < 0f ? 0f : next;
            stock[index] = row;
        }

        public static void AddClamped(
            DynamicBuffer<ResourceAmount> stock,
            DynamicBuffer<ResourceInfo> info,
            in FixedString64Bytes resourceId,
            float delta)
        {
            var index = IndexOf(stock, resourceId);
            if (index < 0 || delta == 0f)
                return;
            var row = stock[index];
            row.Amount = Clamp(row.Amount + delta, StockCap(info, resourceId));
            stock[index] = row;
        }

        public static bool Has(DynamicBuffer<ResourceAmount> stock, in FixedString64Bytes resourceId, float amount)
        {
            return amount <= 0.0001f || Get(stock, resourceId) + 0.0001f >= amount;
        }

        static float Clamp(float amount, float cap)
        {
            if (amount < 0f)
                amount = 0f;
            if (cap > 0.0001f && amount > cap)
                amount = cap;
            return amount;
        }
    }
}
