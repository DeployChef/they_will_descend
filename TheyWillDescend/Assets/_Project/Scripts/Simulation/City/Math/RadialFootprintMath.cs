using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.City
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
            if (!TrySnapRing(center, config, world, out radialIndex, out var turns))
                return false;

            clusterIndex = RadialGridMath.TurnsToCluster(turns, config.GetClusterCount(radialIndex));
            return true;
        }

        /// <summary>
        /// Cursor is the footprint <b>center</b>. Inverse of <see cref="FootprintMarkerPose"/>.
        /// Plaza (inside InnerRadius) still locks to ring 0, angle stays centered.
        /// </summary>
        public static bool TrySnapFootprintCenter(
            float3 center,
            in RadialGridConfig config,
            float3 world,
            in BuildingFootprint footprint,
            out int anchorCluster,
            out int anchorRadial,
            out float centerTurns)
        {
            anchorCluster = 0;
            anchorRadial = 0;
            centerTurns = 0f;
            if (!config.IsValid || !footprint.IsValid)
                return false;

            var delta = world - center;
            var radius = math.length(new float2(delta.x, delta.z));
            centerTurns = RadialGridMath.NormalizedTurns(delta.x, delta.z);

            if (radius >= config.RingLineRadius(config.RingCount))
                return false;

            var maxAnchor = config.RingCount - footprint.DepthRadialRings;
            if (maxAnchor < 0)
                return false;

            if (radius < config.InnerRadius)
            {
                anchorRadial = 0;
            }
            else
            {
                var ringFloat = (radius - config.InnerRadius) / config.RadialStep;
                var anchorFloat = ringFloat - footprint.DepthRadialRings * 0.5f;
                anchorRadial = (int)math.floor(anchorFloat + 0.5f);
                if (anchorRadial < 0)
                    anchorRadial = 0;
                if (anchorRadial > maxAnchor)
                    anchorRadial = maxAnchor;
            }

            var n = config.GetClusterCount(anchorRadial);
            if (n <= 0)
                return false;

            var anchorTurns0 = Fract(centerTurns - footprint.WidthClusters * 0.5f / n);
            anchorCluster = RadialGridMath.TurnsToCluster(anchorTurns0, n);
            return true;
        }

        /// <summary>
        /// Snap only to ring (plaza → ring 0). Angle stays continuous in <paramref name="turns"/>.
        /// </summary>
        public static bool TrySnapRing(
            float3 center,
            in RadialGridConfig config,
            float3 world,
            out int radialIndex,
            out float turns)
        {
            radialIndex = 0;
            turns = 0f;
            if (!config.IsValid)
                return false;

            var delta = world - center;
            var radius = math.length(new float2(delta.x, delta.z));
            turns = RadialGridMath.NormalizedTurns(delta.x, delta.z);

            if (radius < config.InnerRadius)
            {
                radialIndex = 0;
                return true;
            }

            radialIndex = (int)math.floor((radius - config.InnerRadius) / config.RadialStep);
            if (radialIndex < 0)
                radialIndex = 0;
            if (radialIndex >= config.RingCount)
                return false;

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
            var nAnchor = config.GetClusterCount(anchorRadial);
            if (nAnchor <= 0 || anchorCluster < 0 || anchorCluster >= nAnchor)
            {
                clustersOut.Clear();
                return false;
            }

            var turns0 = anchorCluster / (float)nAnchor;
            return TryExpandClustersFromTurns(config, turns0, anchorRadial, footprint, clustersOut);
        }

        public static bool TryExpandClusters(
            in RadialGridConfig config,
            int anchorCluster,
            int anchorRadial,
            in BuildingFootprint footprint,
            NativeList<OccupiedCell> clustersOut)
        {
            var nAnchor = config.GetClusterCount(anchorRadial);
            if (nAnchor <= 0 || anchorCluster < 0 || anchorCluster >= nAnchor)
            {
                clustersOut.Clear();
                return false;
            }

            var turns0 = anchorCluster / (float)nAnchor;
            return TryExpandClustersFromTurns(config, turns0, anchorRadial, footprint, clustersOut);
        }

        /// <summary>
        /// Expand from continuous start angle (turns 0..1). Used when angular snap is off.
        /// </summary>
        public static bool TryExpandClustersFromTurns(
            in RadialGridConfig config,
            float turns0,
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
            if (nAnchor <= 0)
                return false;

            turns0 = Fract(turns0);
            var spanTurns = footprint.WidthClusters / (float)nAnchor;

            for (var d = 0; d < footprint.DepthRadialRings; d++)
            {
                var ring = anchorRadial + d;
                var n = config.GetClusterCount(ring);
                var start = RadialGridMath.TurnsToCluster(turns0, n);
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

        public static bool TryExpandClustersFromTurns(
            in RadialGridConfig config,
            float turns0,
            int anchorRadial,
            in BuildingFootprint footprint,
            NativeList<OccupiedCell> clustersOut)
        {
            clustersOut.Clear();
            if (!config.IsValid || !footprint.IsValid)
                return false;

            if (anchorRadial < 0 || anchorRadial + footprint.DepthRadialRings > config.RingCount)
                return false;

            var nAnchor = config.GetClusterCount(anchorRadial);
            if (nAnchor <= 0)
                return false;

            turns0 = Fract(turns0);
            var spanTurns = footprint.WidthClusters / (float)nAnchor;

            for (var d = 0; d < footprint.DepthRadialRings; d++)
            {
                var ring = anchorRadial + d;
                var n = config.GetClusterCount(ring);
                var start = RadialGridMath.TurnsToCluster(turns0, n);
                var count = math.max(1, (int)math.round(spanTurns * n));

                for (var w = 0; w < count; w++)
                {
                    var c = (start + w) % n;
                    if (c < 0)
                        c += n;
                    clustersOut.Add(new OccupiedCell { Cluster = c, Radial = ring });
                }
            }

            return true;
        }

        public static bool ContainsWorldPoint(
            float3 center,
            in RadialGridConfig config,
            float3 world,
            int anchorCluster,
            int anchorRadial,
            in BuildingFootprint footprint)
        {
            if (!config.IsValid || !footprint.IsValid)
                return false;
            if (anchorRadial < 0 || anchorRadial + footprint.DepthRadialRings > config.RingCount)
                return false;

            var n = config.GetClusterCount(anchorRadial);
            if (n <= 0 || anchorCluster < 0 || anchorCluster >= n)
                return false;

            var delta = world - center;
            var radius = math.length(new float2(delta.x, delta.z));
            var r0 = config.RingLineRadius(anchorRadial);
            var r1 = config.RingLineRadius(anchorRadial + footprint.DepthRadialRings);
            if (radius < r0 || radius > r1)
                return false;

            var turns = RadialGridMath.NormalizedTurns(delta.x, delta.z);
            var span = footprint.WidthClusters / (float)n;
            var dt = Fract(turns - Fract(anchorCluster / (float)n));
            return dt <= span + 1e-4f;
        }

        public static void FootprintMarkerPose(
            float3 center,
            in RadialGridConfig config,
            int anchorCluster,
            int anchorRadial,
            in BuildingFootprint footprint,
            out float3 position,
            out quaternion rotation)
        {
            var nAnchor = config.GetClusterCount(anchorRadial);
            var turns0 = anchorCluster / (float)nAnchor;
            FootprintMarkerPoseFromTurns(
                center, config, turns0, anchorRadial, footprint,
                out position, out rotation);
        }

        public static void FootprintMarkerPoseFromTurns(
            float3 center,
            in RadialGridConfig config,
            float turns0,
            int anchorRadial,
            in BuildingFootprint footprint,
            out float3 position,
            out quaternion rotation)
        {
            var nAnchor = config.GetClusterCount(anchorRadial);
            var midRadial = anchorRadial + (footprint.DepthRadialRings - 1) * 0.5f;
            var turns = Fract(turns0) + footprint.WidthClusters * 0.5f / nAnchor;
            turns = Fract(turns);
            var midRadius = config.InnerRadius + (midRadial + 0.5f) * config.RadialStep;

            var theta = turns * 2f * math.PI;
            var radialDir = new float3(math.sin(theta), 0f, math.cos(theta));
            position = center + radialDir * midRadius;
            rotation = quaternion.LookRotationSafe(radialDir, new float3(0f, 1f, 0f));
        }

        static float Fract(float v)
        {
            v -= math.floor(v);
            if (v < 0f)
                v += 1f;
            return v;
        }
    }
}
