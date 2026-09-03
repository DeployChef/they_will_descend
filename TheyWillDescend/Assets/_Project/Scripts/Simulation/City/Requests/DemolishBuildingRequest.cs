using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    public struct DemolishBuildingRequest : IComponentData
    {
        public int BuildingId;
    }
}