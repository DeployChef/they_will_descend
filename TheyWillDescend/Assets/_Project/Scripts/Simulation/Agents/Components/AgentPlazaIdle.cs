using TheyWillDescend.Simulation.City;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Ring around city center: unassigned always, assigned crew after 18:00.
    /// </summary>
    public struct AgentPlazaIdle : IComponentData
    {
        public float Timer;
        public float Angle;
        public float Radius;
        public byte Walking;
        /// <summary>+1 counterclockwise, -1 clockwise. 0 means not chosen yet.</summary>
        public sbyte Direction;
    }

    /// <summary>
    /// Idle orbit sits on the protected ring-0 road (centerline = InnerRadius).
    /// Lanes stay inside the road width so walkers do not cut across the pyramid.
    /// </summary>
    public static class PlazaRing
    {
        public const float LaneSpacing = 0.22f;

        public static float Center(in RadialGridConfig config) =>
            config.IsValid ? config.RingLineRadius(0) : RadialGridConfig.Default.InnerRadius;

        public static float Lane(in RadialGridConfig config, int salt)
        {
            var lane = salt % 4;
            if (lane < 0)
                lane += 4;
            return Center(config) + (lane - 1.5f) * LaneSpacing;
        }

        public static float Min(in RadialGridConfig config) => Center(config) - 1.5f * LaneSpacing;

        public static float Max(in RadialGridConfig config) => Center(config) + 1.5f * LaneSpacing;

        /// <summary>Outside this, walk straight onto the road before orbiting.</summary>
        public static float Far(in RadialGridConfig config) => Max(config) + 0.75f;

        public static sbyte PickDirection(int salt)
        {
            var bit = ((uint)salt * 2654435761u) & 1u;
            return bit == 0 ? (sbyte)1 : (sbyte)-1;
        }
    }
}
