using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// One polar-aligned edge: same ring (arc) or same fine (ray from center).
    /// </summary>
    public struct RoadSegment : IBufferElementData
    {
        public int Id;
        public Entity Site;
        public int RingA;
        public int FineA;
        public int RingB;
        public int FineB;
        public byte Protected;
    }
}
