using TheyWillDescend.Simulation.City;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    [DisallowMultipleComponent]
    public sealed class BuildingFootprintAuthoring : MonoBehaviour
    {
        [SerializeField] int widthClusters = 2;
        [SerializeField] int depthRadialRings = 2;

        public int WidthClusters => widthClusters > 0 ? widthClusters : 1;
        public int DepthRadialRings => depthRadialRings > 0 ? depthRadialRings : 1;

        public BuildingFootprint Footprint => new()
        {
            WidthClusters = WidthClusters,
            DepthRadialRings = DepthRadialRings
        };
    }
}
