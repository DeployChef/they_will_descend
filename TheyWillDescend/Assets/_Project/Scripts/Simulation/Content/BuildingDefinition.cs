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
    /// Design-time building type. Baker copies numbers onto <see cref="City.BuildingType"/>
    /// and the session catalog. Prefab is mesh only — not the live house.
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
        [SerializeField] [Min(0f)] float constructionDuration;
        [SerializeField] [Min(0)] int workplaceSlots = 1;
        [SerializeField] ResourceDefinition produceResource;
        [SerializeField] [Min(0f)] float producePerSecond = 1f;
        [SerializeField] BuildingCostEntry[] buildCost = Array.Empty<BuildingCostEntry>();
        [SerializeField] GameObject prefab;

        public string TypeId => ContentId.Normalize(typeId, name);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int WidthClusters => widthClusters > 0 ? widthClusters : 1;
        public int DepthRadialRings => depthRadialRings > 0 ? depthRadialRings : 1;
        public float ConstructionDuration => constructionDuration;
        public int WorkplaceSlots => workplaceSlots < 0 ? 0 : workplaceSlots;
        public ResourceDefinition ProduceResource => produceResource;
        public string ProduceResourceId => produceResource != null ? produceResource.ResourceId : string.Empty;
        public float ProducePerSecond => producePerSecond;
        public BuildingCostEntry[] BuildCost => buildCost ?? Array.Empty<BuildingCostEntry>();
        public GameObject Prefab => prefab;

        public BuildingFootprint Footprint => new()
        {
            WidthClusters = WidthClusters,
            DepthRadialRings = DepthRadialRings
        };
    }
}
