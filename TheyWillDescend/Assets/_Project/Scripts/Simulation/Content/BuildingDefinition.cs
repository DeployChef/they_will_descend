using System;
using TheyWillDescend.Simulation.City;
using UnityEngine;

namespace TheyWillDescend.Simulation.Content
{
    [Serializable]
    public struct BuildingCostEntry
    {
        public ResourceDefinition Resource;
        [Min(0f)] public float Amount;
    }

    /// <summary>
    /// Recipe rate on a building type. Unit is <b>per game hour</b> (HUD metric).
    /// Not place cost — that is <see cref="BuildingCostEntry"/>.
    /// </summary>
    [Serializable]
    public struct ResourceRate
    {
        public ResourceDefinition Resource;
        [Min(0f)] public float PerHour;
    }

    /// <summary>
    /// Design-time building type. Baker copies numbers onto the session catalog
    /// and the house stamp. Prefab is mesh only — not the live house.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BuildingDefinition",
        menuName = "They Will Descend/Building Definition")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        [SerializeField] string typeId = "house";
        [SerializeField] string displayName = "House";
        [SerializeField] int widthClusters = 6;
        [SerializeField] int depthRadialRings = 2;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds to raise this house. 0 = appears finished.")]
        float constructionDuration = 8f;
        [SerializeField] [Min(0)] int workplaceSlots = 10;
        [SerializeField] ResourceRate[] recipeInputs = Array.Empty<ResourceRate>();
        [SerializeField] ResourceRate[] recipeOutputs = Array.Empty<ResourceRate>();
        [SerializeField] BuildingCostEntry[] buildCost = Array.Empty<BuildingCostEntry>();
        [SerializeField] GameObject prefab;

        public string TypeId => ContentId.Normalize(typeId, name);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int WidthClusters => widthClusters > 0 ? widthClusters : 1;
        public int DepthRadialRings => depthRadialRings > 0 ? depthRadialRings : 1;
        public float ConstructionDuration => constructionDuration < 0f ? 0f : constructionDuration;
        public int WorkplaceSlots => workplaceSlots < 0 ? 0 : workplaceSlots;
        public ResourceRate[] RecipeInputs => recipeInputs ?? Array.Empty<ResourceRate>();
        public ResourceRate[] RecipeOutputs => recipeOutputs ?? Array.Empty<ResourceRate>();
        public BuildingCostEntry[] BuildCost => buildCost ?? Array.Empty<BuildingCostEntry>();
        public GameObject Prefab => prefab;

        public BuildingFootprint Footprint => new()
        {
            WidthClusters = WidthClusters,
            DepthRadialRings = DepthRadialRings
        };
    }
}
