using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Polar span of one billed road section. Lives on the construction-site entity.
    /// </summary>
    public struct RoadSpan : IComponentData
    {
        public int RingA;
        public int FineA;
        public int RingB;
        public int FineB;
    }
}
