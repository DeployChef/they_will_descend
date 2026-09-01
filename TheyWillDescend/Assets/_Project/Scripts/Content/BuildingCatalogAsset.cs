using System.Collections.Generic;
using TheyWillDescend.Simulation.Content;
using UnityEngine;

namespace TheyWillDescend.Content
{
    /// <summary>
    /// Art registry: typeId → house prefab. Same asset on
    /// <c>BuildingCatalogAuthoring</c> (bake copies digits into World) and
    /// Game view/ghost (Instantiate). Simulation does not reference this type.
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
                var stamp = entry.GetComponent<BuildingStamp>();
                if (stamp == null || stamp.TypeId != id)
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
