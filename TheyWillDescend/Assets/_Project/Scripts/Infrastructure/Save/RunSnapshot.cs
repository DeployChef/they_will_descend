using System;

namespace _Project.Scripts.Infrastructure.Save
{
    [Serializable]
    public sealed class RunSnapshot
    {
        public int version = 2;
        public int speed = 1;
        public bool playerPaused;
        public int day;
        public float elapsedInDay;
        public float dayDuration = 5f;
        public AgentSnapshot[] agents = Array.Empty<AgentSnapshot>();
        public BuildingSnapshot[] buildings = Array.Empty<BuildingSnapshot>();
    }

    [Serializable]
    public sealed class AgentSnapshot
    {
        public string prefabId;
        public float posX;
        public float posY;
        public float posZ;
        public float fwdX;
        public float fwdY;
        public float fwdZ;
        // Temporary: circle is a movement behavior, not the character. Drop when pathing exists.
        public float centerX;
        public float centerY;
        public float centerZ;
        public float radius;
        public float speed;
        public float direction;
        public float angleRadians;
    }

    [Serializable]
    public sealed class BuildingSnapshot
    {
        public int widthClusters;
        public int depthRadialRings;
        public int anchorCluster;
        public int anchorRadial;
    }
}
