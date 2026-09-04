using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Session lifecycle command that removes runtime buildings and resets grid occupancy.
    /// </summary>
    public struct DespawnAllBuildingsCommand : IBufferElementData
    {
        public byte Requested;
    }
}
