using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Session road graph on the radial grid (arc along a ring, or ray from center).
    /// </summary>
    public struct RoadNetwork : IComponentData
    {
        public const string TypeId = "road";

        public float SectionLength;
        public int NextSegmentId;
    }
}
