using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Player intent: unassign staff and start dismantling. Crew and refund are sim rules.
    /// </summary>
    public struct DeconstructBuildingCommand : IBufferElementData
    {
        public int BuildingId;
    }
}
