using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    public struct PlaceRoadStrokeRequest : IComponentData
    {
        public int ValidPointCount;
    }

    public struct RoadStrokePoint : IBufferElementData
    {
        public int Ring;
        public int Fine;
    }
}
