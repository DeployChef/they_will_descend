using System;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using UnityEngine;

namespace TheyWillDescend.Content
{
    /// <summary>
    /// Canonical default description on a building prefab. Catalog bake copies
    /// these numbers into immutable base rows; run setup resolves overlays into
    /// the runtime catalog. Presentation metadata stays on BuildingView.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingStamp : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] string typeId;

        [Header("Always")]
        [SerializeField] int widthClusters = 2;
        [SerializeField] int depthRadialRings = 2;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds to raise this house. 0 = appears finished.")]
        float constructionDuration = 8f;
        [SerializeField]
        [Min(1)]
        [Tooltip("Max workers who walk to this site. Progress starts when the first arrives.")]
        int constructionCrewSlots = ConstructionCrew.DefaultSlots;
        [SerializeField] BuildingCostEntry[] costs = Array.Empty<BuildingCostEntry>();

        [Header("Workplace")]
        [SerializeField] bool workplace;
        [SerializeField] [Min(0)] int workplaceSlots = 10;

        [Header("Recipe")]
        [SerializeField] bool recipe;
        [SerializeField] ResourceRate[] recipeInputs = Array.Empty<ResourceRate>();
        [SerializeField] ResourceRate[] recipeOutputs = Array.Empty<ResourceRate>();

        public string TypeId => ContentId.Normalize(typeId, name);

        public int WidthClusters => widthClusters > 0 ? widthClusters : 1;

        public int DepthRadialRings => depthRadialRings > 0 ? depthRadialRings : 1;

        public float ConstructionDuration => constructionDuration < 0f ? 0f : constructionDuration;

        public int ConstructionCrewSlots => ConstructionCrew.ResolveSlots(constructionCrewSlots);

        public BuildingCostEntry[] Costs => costs ?? Array.Empty<BuildingCostEntry>();

        public bool HasWorkplace => workplace && workplaceSlots > 0;

        public int WorkplaceSlots => HasWorkplace ? workplaceSlots : 0;

        public bool HasRecipe => recipe && (HasAnyRate(recipeInputs) || HasAnyRate(recipeOutputs));

        public ResourceRate[] RecipeInputs => recipeInputs ?? Array.Empty<ResourceRate>();

        public ResourceRate[] RecipeOutputs => recipeOutputs ?? Array.Empty<ResourceRate>();

        public BuildingFootprint Footprint => new()
        {
            WidthClusters = WidthClusters,
            DepthRadialRings = DepthRadialRings
        };

        static bool HasAnyRate(ResourceRate[] rates)
        {
            if (rates == null)
                return false;
            for (var i = 0; i < rates.Length; i++)
            {
                if (rates[i].Resource != null && rates[i].PerHour > 0.0001f)
                    return true;
            }

            return false;
        }
    }
}
