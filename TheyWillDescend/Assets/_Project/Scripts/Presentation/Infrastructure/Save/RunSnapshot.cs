using System;

namespace TheyWillDescend.Infrastructure.Save
{
    [Serializable]
    public sealed class RunSnapshot
    {
        /// <summary>
        /// Current payload only. Older slots are deleted on load — no migration while we iterate.
        /// </summary>
        public const int CurrentVersion = 16;

        public int version = CurrentVersion;
        public int speed = 1;
        public bool playerPaused;
        public int day;
        public float elapsedInDay;
        public float dayDuration = 5f;
        public ResourceSnapshot[] resources = Array.Empty<ResourceSnapshot>();
        public AgentSnapshot[] agents = Array.Empty<AgentSnapshot>();
        public BuildingSnapshot[] buildings = Array.Empty<BuildingSnapshot>();
    }

    [Serializable]
    public sealed class ResourceSnapshot
    {
        public string resourceId;
        public float amount;
    }

    [Serializable]
    public sealed class AgentSnapshot
    {
        public byte agentType;
        public int agentId;
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
        public int workplaceBuildingId;
        public byte arrived;
        public byte plazaWalking;
        public float plazaTimer;
        public float plazaAngle;
        public float plazaRadius;
    }

    [Serializable]
    public sealed class BuildingSnapshot
    {
        public int widthClusters;
        public int depthRadialRings;
        public int anchorCluster;
        public int anchorRadial;
        public int id;
        public string typeId;
        public byte built;
        public float constructionElapsed;
        public float constructionDuration;
        public int workerAgentId;
    }
}
