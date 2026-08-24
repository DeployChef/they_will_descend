using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Instance on the grid. Size and recipe live on <see cref="BuildingType"/> / catalog.
    /// Width/depth are copied at Place so occupancy/views do not resolve the catalog.
    /// </summary>
    public struct Building : IComponentData
    {
        public int Id;
        public FixedString64Bytes TypeId;
        public int WidthClusters;
        public int DepthRadialRings;
        public int AnchorCluster;
        public int AnchorRadial;
    }
}
