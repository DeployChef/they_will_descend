using Unity.Mathematics;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Cluster/ring polar math. No fine micro-grid — roads later can be freer.
    /// </summary>
    public static class RadialGridMath
    {
        public static float OuterRadius(in RadialGridConfig config) =>
            config.RingLineRadius(config.RingCount);

        public static float NormalizedTurns(float dx, float dz)
        {
            var angle = math.atan2(dx, dz);
            var turns = angle / (2f * math.PI);
            if (turns < 0f)
                turns += 1f;
            return turns;
        }

        public static int TurnsToCluster(float turns, int clusterCount)
        {
            if (clusterCount <= 0)
                return 0;
            var index = (int)math.floor(turns * clusterCount);
            if (index >= clusterCount)
                index = 0;
            return index;
        }

        public static float ClusterCenterTurns(int clusterIndex, int clusterCount) =>
            (clusterIndex + 0.5f) / clusterCount;

        public static float3 PolarToWorld(float3 center, float turns, float radius)
        {
            var theta = turns * 2f * math.PI;
            return center + new float3(math.sin(theta), 0f, math.cos(theta)) * radius;
        }
    }
}
