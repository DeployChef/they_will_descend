using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    public struct DemolishRoadRequest : IComponentData
    {
        public int SegmentId;
    }
}
