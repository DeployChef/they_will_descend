using System.Collections.Generic;
using UnityEngine;

namespace TheyWillDescend.Simulation.Content
{
    /// <summary>
    /// One document for bake, scenario editor, HUD, and ghost. Assign the same asset
    /// on BuildingCatalogAuthoring (SubScene) and BuildPlacementController (Game).
    /// </summary>
    [CreateAssetMenu(
        fileName = "BuildingCatalog",
        menuName = "They Will Descend/Building Catalog")]
    public sealed class BuildingCatalogAsset : ScriptableObject
    {
        [SerializeField] BuildingDefinition[] buildings = System.Array.Empty<BuildingDefinition>();

        public IReadOnlyList<BuildingDefinition> Buildings => buildings;

        public bool TryGet(string typeId, out BuildingDefinition definition)
        {
            definition = null;
            var id = ContentId.Normalize(typeId);
            if (buildings == null || string.IsNullOrEmpty(id))
                return false;
            for (var i = 0; i < buildings.Length; i++)
            {
                var entry = buildings[i];
                if (entry == null || entry.TypeId != id)
                    continue;
                definition = entry;
                return true;
            }

            return false;
        }

        public GameObject FindPrefab(string typeId)
        {
            return TryGet(typeId, out var definition) ? definition.Prefab : null;
        }
    }
}
