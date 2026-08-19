using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// Singleton for UI↔sim IO. Commands in, events out. Not presentation.
    /// </summary>
    public struct SimBridge : IComponentData
    {
        public int NextAgentId;
        public byte DespawnAllAgents;
        public byte DespawnAllBuildings;
    }
}
