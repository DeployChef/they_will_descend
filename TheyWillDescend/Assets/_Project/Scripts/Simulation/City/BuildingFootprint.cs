using System;

namespace _Project.Scripts.Simulation.City
{
    /// <summary>
    /// Building size in logical clusters × rings.
    /// Width is angular clusters of the <b>anchor ring</b> (world width ≈ const).
    /// </summary>
    [Serializable]
    public struct BuildingFootprint
    {
        public int WidthClusters;
        public int DepthRadialRings;

        /// <summary>FP calibration: 11 such houses fit on rings 0–1.</summary>
        public static BuildingFootprint House6x2 => new()
        {
            WidthClusters = 6,
            DepthRadialRings = 2
        };

        public static BuildingFootprint Cube2x2 => new()
        {
            WidthClusters = 2,
            DepthRadialRings = 2
        };

        public bool IsValid => WidthClusters > 0 && DepthRadialRings > 0;
    }
}
