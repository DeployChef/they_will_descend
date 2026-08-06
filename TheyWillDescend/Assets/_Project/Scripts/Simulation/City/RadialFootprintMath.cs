using System.Collections.Generic;
using Unity.Mathematics;

namespace _Project.Scripts.Simulation.City
{
    /// <summary>
    /// Snap / expand / pose. Building width is locked to TargetClusterWorldWidth
    /// so stubs stay ~same size on every ring.
    /// </summary>
    public static class RadialFootprintMath
    {
        public static bool TrySnapAnchor(
            float3 center,
            in RadialGridConfig config,
            float3 world,
            out int clusterIndex,
            out int radialIndex)
        {
            clusterIndex = 0;
            radialIndex = 0;
            if (!config.IsValid)
                return false;

            var delta = world - center;
            var radius = math.length(new float2(delta.x, delta.z));
            var turns = RadialGridMath.NormalizedTurns(delta.x, delta.z);

            if (radius < config.InnerRadius)
            {
                radialIndex = 0;
                clusterIndex = RadialGridMath.TurnsToCluster(turns, config.GetClusterCount(0));
                return true;
            }

            radialIndex = (int)math.floor((radius - config.InnerRadius) / config.RadialStep);
            if (radialIndex < 0)
                radialIndex = 0;
            if (radialIndex >= config.RingCount)
                return false;

            clusterIndex = RadialGridMath.TurnsToCluster(turns, config.GetClusterCount(radialIndex));
            return true;
        }

        /// <summary>
        /// Occupied cluster slots. Angular span taken from anchor ring;
        /// on denser outer rings the same world arc covers more clusters.
        /// </summary>
        public static bool TryExpandClusters(
            in RadialGridConfig config,
            int anchorCluster,
            int anchorRadial,
            in BuildingFootprint footprint,
            List<(int cluster, int radial)> clustersOut)
        {
            clustersOut.Clear();
            if (!config.IsValid || !footprint.IsValid)
                return false;

            if (anchorRadial < 0 || anchorRadial + footprint.DepthRadialRings > config.RingCount)
                return false;

            var nAnchor = config.GetClusterCount(anchorRadial);
            if (anchorCluster < 0 || anchorCluster >= nAnchor)
                return false;

            var turns0 = anchorCluster / (float)nAnchor;
            var spanTurns = footprint.WidthClusters / (float)nAnchor;

            for (var d = 0; d < footprint.DepthRadialRings; d++)
            {
                var ring = anchorRadial + d;
                var n = config.GetClusterCount(ring);
                var start = RadialGridMath.TurnsToCluster(turns0, n);
                // How many clusters on this ring cover the same world arc.
                var count = math.max(1, (int)math.round(spanTurns * n));

                for (var w = 0; w < count; w++)
                {
                    var c = (start + w) % n;
                    if (c < 0)
                        c += n;
                    clustersOut.Add((c, ring));
                }
            }

            return true;
        }

        public static void FootprintToWorldPose(
            float3 center,
            in RadialGridConfig config,
            int anchorCluster,
            int anchorRadial,
            in BuildingFootprint footprint,
            out float3 position,
            out quaternion rotation,
            out float3 scale)
        {
            var nAnchor = config.GetClusterCount(anchorRadial);
            var midCluster = anchorCluster + (footprint.WidthClusters - 1) * 0.5f;
            var midRadial = anchorRadial + (footprint.DepthRadialRings - 1) * 0.5f;

            var turns = RadialGridMath.ClusterCenterTurns(
                (int)math.floor(midCluster),
                nAnchor);
            // Better: interpolate mid of footprint in turns.
            turns = (anchorCluster + footprint.WidthClusters * 0.5f) / nAnchor;
            var midRadius = config.InnerRadius + (midRadial + 0.5f) * config.RadialStep;

            var theta = turns * 2f * math.PI;
            var radialDir = new float3(math.sin(theta), 0f, math.cos(theta));
            position = center + radialDir * midRadius;
            rotation = quaternion.LookRotationSafe(radialDir, new float3(0f, 1f, 0f));

            // Same world size on every ring (calibrated from ring 0).
            var arcWidth = footprint.WidthClusters * config.TargetClusterWorldWidth;
            var radialDepth = footprint.DepthRadialRings * config.RadialStep;
            var height = math.min(arcWidth, radialDepth) * 0.55f;
            scale = new float3(
                math.max(0.2f, arcWidth),
                math.max(0.2f, height),
                math.max(0.2f, radialDepth));
        }
    }
}
