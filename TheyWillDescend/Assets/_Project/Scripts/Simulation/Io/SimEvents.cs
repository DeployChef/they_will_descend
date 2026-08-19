using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Io
{
    public struct AgentSpawnedEvent : IBufferElementData
    {
        public int AgentId;
        public float3 Position;
        public FixedString64Bytes VisualId;
    }

    public struct AgentDespawnedEvent : IBufferElementData
    {
        public int AgentId;
    }

    public struct DayChangedEvent : IBufferElementData
    {
        public int Day;
    }

    public struct BuildingPlacedEvent : IBufferElementData
    {
        public int BuildingId;
        public int WidthClusters;
        public int DepthRadialRings;
        public int AnchorCluster;
        public int AnchorRadial;
    }

    public struct BuildingDespawnedEvent : IBufferElementData
    {
        public int BuildingId;
    }

    public struct BuildingRejectedEvent : IBufferElementData
    {
        public int AnchorCluster;
        public int AnchorRadial;
    }
}
