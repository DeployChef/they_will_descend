using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    public enum PlaceBuildingCommandSource : byte
    {
        Gameplay = 0,
        SnapshotRestore = 1
    }

    public struct PlaceBuildingCommand : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public int WidthClusters;
        public int DepthRadialRings;
        public int AnchorCluster;
        public int AnchorRadial;
        public float ConstructionElapsed;
        public float ConstructionDuration;
        public byte InstantComplete;
        public int BuildingId;
        public PlaceBuildingCommandSource Source;
    }
}
