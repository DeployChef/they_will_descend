using System;

namespace TheyWillDescend.Infrastructure.Save
{
    [Serializable]
    public sealed class RunSnapshot
    {
        /// <summary>
        /// v7: buildings include built + construction elapsed/duration.
        /// Older files load houses as already finished.
        /// </summary>
        public const int CurrentVersion = 7;

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
        public byte built;
        public float constructionElapsed;
        public float constructionDuration;
    }
}
