using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    /// <summary>
    /// Read stamp modules from a catalog prefab (editor + baker helpers).
    /// </summary>
    public static class BuildingStampRead
    {
        public static string TypeId(GameObject prefab)
        {
            if (prefab == null)
                return string.Empty;
            var key = prefab.GetComponent<BuildingKey>();
            return key != null ? key.TypeId : string.Empty;
        }

        public static bool TryFootprint(GameObject prefab, out BuildingFootprint footprint)
        {
            footprint = default;
            if (prefab == null)
                return false;
            var module = prefab.GetComponent<BuildingFootprintAuthoring>();
            if (module == null)
                return false;
            footprint = module.Footprint;
            return footprint.IsValid;
        }

        public static float MeshSize(GameObject prefab)
        {
            return BuildingPrefabMetrics.HorizontalSize(prefab);
        }
    }
}
