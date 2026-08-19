using System;

namespace TheyWillDescend.Infrastructure.Save
{
    [Serializable]
    public sealed class RunSnapshot
    {
        /// <summary>
        /// v10: agent motor target is a world point, not a building id.
        /// v9: agents stored target building id + moving; buildings store Id.
        /// v8: agents store pose + walk speed.
        /// v7: buildings include built + construction elapsed/duration.
        /// </summary>
        public const int CurrentVersion = 10;

        public int version = CurrentVersion;
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
        public byte agentType;
        public float posX;
        public float posY;
        public float posZ;
        public float fwdX;
        public float fwdY;
        public float fwdZ;
        public float speed;
        public float targetX;
        public float targetY;
        public float targetZ;
        public byte moving;
    }

    [Serializable]
    public sealed class BuildingSnapshot
    {
        public int widthClusters;
        public int depthRadialRings;
        public int anchorCluster;
        public int anchorRadial;
        public int id;
        public byte built;
        public float constructionElapsed;
        public float constructionDuration;
    }
}
