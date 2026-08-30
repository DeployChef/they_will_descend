using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using TheyWillDescend.Simulation.Economy;

namespace TheyWillDescend.Simulation.Gods
{
    /// <summary>
    /// Per-resource slider on HQ. PerHour is the HUD unit.
    /// </summary>
    public struct PyramidFeedLine : IBufferElementData
    {
        public FixedString64Bytes ResourceId;
        public float PerHour;
    }

    public static class PyramidFeed
    {
        public static int IndexOf(DynamicBuffer<PyramidFeedLine> feed, in FixedString64Bytes resourceId)
        {
            for (var i = 0; i < feed.Length; i++)
            {
                if (feed[i].ResourceId == resourceId)
                    return i;
            }

            return -1;
        }

        public static bool IsTribute(
            DynamicBuffer<EraTributeLine> tribute,
            int eraIndex,
            in FixedString64Bytes resourceId)
        {
            for (var i = 0; i < tribute.Length; i++)
            {
                var row = tribute[i];
                if (row.EraIndex == eraIndex && row.ResourceId == resourceId)
                    return true;
            }

            return false;
        }

        public static float UnitEnergy(
            DynamicBuffer<ResourceInfo> info,
            DynamicBuffer<EraTributeLine> tribute,
            DynamicBuffer<EraLine> eras,
            int eraIndex,
            in FixedString64Bytes resourceId)
        {
            var value = ResourceLedger.EnergyValue(info, resourceId);
            if (value <= 0.0001f)
                return 0f;
            if (!IsTribute(tribute, eraIndex, resourceId) || eraIndex < 0 || eraIndex >= eras.Length)
                return value;
            var mul = eras[eraIndex].TributeEnergyMul;
            if (mul < 0.0001f)
                mul = 1f;
            return value * mul;
        }

        public static float TotalEnergyPerHour(
            DynamicBuffer<PyramidFeedLine> feed,
            DynamicBuffer<ResourceInfo> info,
            DynamicBuffer<EraTributeLine> tribute,
            DynamicBuffer<EraLine> eras,
            int eraIndex)
        {
            var total = 0f;
            for (var i = 0; i < feed.Length; i++)
            {
                var line = feed[i];
                if (line.PerHour <= 0.0001f)
                    continue;
                total += line.PerHour * UnitEnergy(info, tribute, eras, eraIndex, line.ResourceId);
            }

            return total;
        }

        public static void SetPerHour(
            DynamicBuffer<PyramidFeedLine> feed,
            DynamicBuffer<ResourceInfo> info,
            in FixedString64Bytes resourceId,
            float perHour)
        {
            var index = IndexOf(feed, resourceId);
            if (index < 0)
                return;
            if (!ResourceLedger.CanFeed(info, resourceId))
                return;

            var line = feed[index];
            line.PerHour = math.max(0f, perHour);
            feed[index] = line;
        }
    }
}
