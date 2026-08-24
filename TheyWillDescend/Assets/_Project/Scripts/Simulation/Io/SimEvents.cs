using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// One-shot facts the HUD cannot reconstruct by pulling state (toast, reject).
    /// Spawn/despawn are not events: views pull which entities exist.
    /// </summary>
    public struct BuildingRejectedEvent : IBufferElementData
    {
        public const byte UnknownType = 1;
        public const byte InvalidCell = 2;
        public const byte Overlap = 3;
        public const byte Unaffordable = 4;

        public int AnchorCluster;
        public int AnchorRadial;
        public byte Reason;
    }
}
