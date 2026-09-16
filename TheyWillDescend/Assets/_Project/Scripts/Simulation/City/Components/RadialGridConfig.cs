using System;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// FP-like polar underlay: NO micro fine grid.
    /// Cluster count per ring is chosen so cluster world width ≈ const
    /// (calibrated on rings 0–1: 33 clusters = 11 houses × width 3).
    /// InnerRadius sits outside the pyramid mesh (~7.9 m XZ) so ring 0 is a walkway.
    /// </summary>
    [Serializable]
    public struct RadialGridConfig
    {
        public float InnerRadius;

        /// <summary>Ring band thickness (keep relatively small).</summary>
        public float RadialStep;

        public int RingCount;

        /// <summary>Clusters on rings 0–1 (11 × house width 3).</summary>
        public int InnerBandClusterCount;

        public static RadialGridConfig Default => new()
        {
            InnerRadius = 9f,
            RadialStep = 2.025f,
            RingCount = 20,
            InnerBandClusterCount = 33
        };

        public bool IsValid =>
            InnerRadius >= 0f
            && RadialStep > 0f
            && RingCount > 0
            && InnerBandClusterCount > 0;

        public float RingLineRadius(int ringNumber) =>
            InnerRadius + ringNumber * RadialStep;

        public float RingMidRadius(int ringIndex) =>
            InnerRadius + (ringIndex + 0.5f) * RadialStep;

        /// <summary>
        /// Target world width of one cluster, taken from the inner band.
        /// All rings aim for approximately this width.
        /// </summary>
        public float TargetClusterWorldWidth
        {
            get
            {
                var r = RingMidRadius(0);
                return (float)(2d * Math.PI * r / InnerBandClusterCount);
            }
        }

        /// <summary>
        /// How many clusters around this ring so each ≈ TargetClusterWorldWidth.
        /// Rings 0–1 forced to InnerBandClusterCount; further rings use the same
        /// band pairing (every 2 rings share one count).
        /// </summary>
        public int GetClusterCount(int ring)
        {
            if (ring < 0)
                ring = 0;

            if (ring < 2)
                return InnerBandClusterCount;

            var bandStart = (ring / 2) * 2;
            var r = RingMidRadius(bandStart);
            var count = (int)Math.Round(2d * Math.PI * r / TargetClusterWorldWidth);
            if (count < InnerBandClusterCount)
                count = InnerBandClusterCount;
            // Prefer packing that tiles width-3 houses.
            var rem = count % 3;
            if (rem != 0)
                count += 3 - rem;
            return count;
        }

        public float ClusterWorldWidth(int ring)
        {
            var n = GetClusterCount(ring);
            if (n <= 0)
                return TargetClusterWorldWidth;
            return (float)(2d * Math.PI * RingMidRadius(ring) / n);
        }
    }
}
