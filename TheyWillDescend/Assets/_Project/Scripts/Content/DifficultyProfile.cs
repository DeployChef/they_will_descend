using System;
using System.Collections.Generic;
using TheyWillDescend.Simulation.Content;
using UnityEngine;

namespace TheyWillDescend.Content
{
    /// <summary>
    /// Overlay on the baked stamp snapshot. Empty flags keep the prefab default.
    /// Same shape as a future A/B pack: replace fields by typeId, do not clone prefabs.
    /// </summary>
    [Serializable]
    public struct DifficultyBuildingOverride
    {
        public string typeId;
        public bool replaceConstruction;
        [Min(0f)] public float constructionDuration;
        public bool replaceSlots;
        [Min(0)] public int workplaceSlots;
        public bool replaceCosts;
        public BuildingCostEntry[] costs;
        public bool replaceRecipe;
        public ResourceRate[] recipeInputs;
        public ResourceRate[] recipeOutputs;

        public string TypeId => ContentId.Normalize(typeId);
    }

    [CreateAssetMenu(
        fileName = "DifficultyProfile",
        menuName = "They Will Descend/Difficulty Profile")]
    public sealed class DifficultyProfile : ScriptableObject
    {
        [SerializeField] DifficultyBuildingOverride[] buildings = Array.Empty<DifficultyBuildingOverride>();

        public IReadOnlyList<DifficultyBuildingOverride> Buildings =>
            buildings ?? Array.Empty<DifficultyBuildingOverride>();
    }
}
