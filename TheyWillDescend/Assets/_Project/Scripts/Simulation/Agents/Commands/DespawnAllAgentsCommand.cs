using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Session lifecycle command that removes all runtime agents and resets their ID sequence.
    /// </summary>
    public struct DespawnAllAgentsCommand : IBufferElementData
    {
        public byte Requested;
    }
}
