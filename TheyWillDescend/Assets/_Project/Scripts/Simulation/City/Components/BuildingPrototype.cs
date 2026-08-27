using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Session catalog of bake-time house stamps. Place / construction look up
    /// <see cref="Prefab"/>. No GameObject handles — those stay on the catalog asset.
    /// </summary>
    public struct BuildingPrototype : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public Entity Prefab;
        public FixedString64Bytes DisplayName;
        public int WidthClusters;
        public int DepthRadialRings;
        public float MeshSize;
        public float ConstructionDuration;
        public int WorkplaceSlots;

        public BuildingFootprint Footprint => new()
        {
            WidthClusters = WidthClusters,
            DepthRadialRings = DepthRadialRings
        };
    }

    public static class BuildingCatalog
    {
        public static bool TryResolve(
            DynamicBuffer<BuildingPrototype> catalog,
            in FixedString64Bytes typeId,
            int widthClusters,
            int depthRadialRings,
            out BuildingPrototype prototype)
        {
            prototype = default;
            if (catalog.Length == 0)
                return false;

            if (!typeId.IsEmpty)
            {
                for (var i = 0; i < catalog.Length; i++)
                {
                    if (catalog[i].TypeId != typeId || catalog[i].Prefab == Entity.Null)
                        continue;
                    prototype = catalog[i];
                    return true;
                }

                return false;
            }

            for (var i = 0; i < catalog.Length; i++)
            {
                var entry = catalog[i];
                if (entry.Prefab == Entity.Null)
                    continue;
                if (entry.WidthClusters != widthClusters || entry.DepthRadialRings != depthRadialRings)
                    continue;
                prototype = entry;
                return true;
            }

            return false;
        }
    }
}
