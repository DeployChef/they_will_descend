using System;

namespace TheyWillDescend.Infrastructure.Save
{
    [Serializable]
    public sealed class RunSnapshot
    {
        /// <summary>
        /// Current payload only. Older slots are deleted on load — no migration while we iterate.
        /// </summary>
        public const int CurrentVersion = 24;

        public int version = CurrentVersion;
        public int speed = 1;
        /// <summary>HUD clock pause. Esc overlay is not saved.</summary>
        public bool playerPaused;
        public int day;
        public float elapsedInDay;
        public float dayDuration = 60f;
        public ResourceSnapshot[] resources = Array.Empty<ResourceSnapshot>();
        public AgentSnapshot[] agents = Array.Empty<AgentSnapshot>();
        public BuildingSnapshot[] buildings = Array.Empty<BuildingSnapshot>();
        public ResolvedBuildingPrototypeSnapshot[] buildingCatalog =
            Array.Empty<ResolvedBuildingPrototypeSnapshot>();
        public ResolvedBuildingCostSnapshot[] buildingCosts =
            Array.Empty<ResolvedBuildingCostSnapshot>();
        public ResolvedBuildingRecipeSnapshot[] buildingRecipes =
            Array.Empty<ResolvedBuildingRecipeSnapshot>();
        public float faith;
        public float faithMax;
        public int eraIndex;
        public int eraStartDay;
        public float eraStartElapsed;
        public float previousMaxLoyalty;
        public float targetMaxLoyalty;
        public PyramidFeedSnapshot[] pyramidFeed = Array.Empty<PyramidFeedSnapshot>();
        public string activeTechId = string.Empty;
        public ResearchLineSnapshot[] research = Array.Empty<ResearchLineSnapshot>();
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
        public byte dismantling;
        public int workerAgentId;
        public byte paused;
    }

    [Serializable]
    public sealed class ResolvedBuildingPrototypeSnapshot
    {
        public string typeId;
        public int widthClusters;
        public int depthRadialRings;
        public float constructionDuration;
        public int constructionCrewSlots;
        public int workplaceSlots;
        public byte researchWorkplace;
        public byte requiresUnlock;
    }

    [Serializable]
    public sealed class ResolvedBuildingCostSnapshot
    {
        public string typeId;
        public string resourceId;
        public float amount;
    }

    [Serializable]
    public sealed class ResolvedBuildingRecipeSnapshot
    {
        public string typeId;
        public byte kind;
        public string resourceId;
        public float perHour;
    }

    [Serializable]
    public sealed class ResearchLineSnapshot
    {
        public string techId;
        public float accumulatedHours;
        public byte completed;
        public byte costPaid;
    }

    [Serializable]
    public sealed class PyramidFeedSnapshot
    {
        public string resourceId;
        public float perHour;
    }
}
