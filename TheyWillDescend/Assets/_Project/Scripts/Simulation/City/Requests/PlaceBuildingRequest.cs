using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    public enum PlaceBuildingCommandSource : byte
    {
        Gameplay = 0,
        SnapshotRestore = 1,
        Setup = 2
    }

    public struct PlaceBuildingRequest : IComponentData

    {
        public FixedString64Bytes TypeId;
        public int WidthClusters;
        public int DepthRadialRings;
        public int AnchorCluster;
        public int AnchorRadial;
        public float ConstructionElapsed;
        public float ConstructionDuration;
        public byte InstantComplete;
        public byte Dismantling;
        public int BuildingId;
        public PlaceBuildingCommandSource Source;
        public int DesiredWorkers;
        public byte Paused;
    }
}