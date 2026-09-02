using TheyWillDescend.Content;
using TheyWillDescend.Simulation.City;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    /// <summary>
    /// Read stamp card from a catalog prefab (editor + baker helpers).
    /// </summary>
    public static class BuildingStampRead
    {
        public static string TypeId(GameObject prefab)
        {
            if (prefab == null)
                return string.Empty;
            var stamp = prefab.GetComponent<BuildingStamp>();
            return stamp != null ? stamp.TypeId : string.Empty;
        }

        public static bool TryFootprint(GameObject prefab, out BuildingFootprint footprint)
        {
            footprint = default;
            if (prefab == null)
                return false;
            var stamp = prefab.GetComponent<BuildingStamp>();
            if (stamp == null)
                return false;
            footprint = stamp.Footprint;
            return footprint.IsValid;
        }
    }
}
