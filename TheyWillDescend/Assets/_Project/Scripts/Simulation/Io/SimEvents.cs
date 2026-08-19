using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// One-shot facts the HUD cannot reconstruct by pulling state (toast, reject).
    /// Spawn/despawn are not events: views pull which entities exist.
    /// </summary>
    public struct BuildingRejectedEvent : IBufferElementData
    {
        public int AnchorCluster;
        public int AnchorRadial;
    }
}
