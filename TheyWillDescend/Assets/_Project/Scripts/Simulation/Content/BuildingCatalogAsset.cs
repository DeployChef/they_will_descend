using System.Collections.Generic;
using UnityEngine;

namespace TheyWillDescend.Simulation.Content
{
    /// <summary>
    /// List of house prefabs that exist in this build. Same asset on
    /// BuildingCatalogAuthoring (SubScene) and BuildPlacementController (Game).
    /// </summary>
    [CreateAssetMenu(
        fileName = "BuildingCatalog",
        menuName = "They Will Descend/Building Catalog")]
    public sealed class BuildingCatalogAsset : ScriptableObject
    {
        [SerializeField] GameObject[] buildings = System.Array.Empty<GameObject>();

        public IReadOnlyList<GameObject> Prefabs => buildings;

        public bool TryGet(string typeId, out GameObject prefab)
        {
            prefab = null;
            var id = ContentId.Normalize(typeId);
            if (buildings == null || string.IsNullOrEmpty(id))
                return false;
            for (var i = 0; i < buildings.Length; i++)
            {
                var entry = buildings[i];
                if (entry == null)
                    continue;
                var key = entry.GetComponent<BuildingKey>();
                if (key == null || key.TypeId != id)
                    continue;
                prefab = entry;
                return true;
            }

            return false;
        }

        public GameObject FindPrefab(string typeId)
        {
            return TryGet(typeId, out var prefab) ? prefab : null;
        }
    }
}
