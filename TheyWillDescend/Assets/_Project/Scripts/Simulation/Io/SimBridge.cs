using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// Baked with SimControl. Command buffers in, reject events out. Not presentation.
    /// </summary>
    public struct SimBridge : IComponentData
    {
        public int NextAgentId;
        public byte DespawnAllAgents;
        public byte DespawnAllBuildings;
    }
}
