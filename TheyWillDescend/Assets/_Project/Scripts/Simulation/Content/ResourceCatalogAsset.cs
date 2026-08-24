using System.Collections.Generic;
using UnityEngine;

namespace TheyWillDescend.Simulation.Content
{
    [CreateAssetMenu(
        fileName = "ResourceCatalog",
        menuName = "They Will Descend/Resource Catalog")]
    public sealed class ResourceCatalogAsset : ScriptableObject
    {
        [SerializeField] ResourceDefinition[] resources = System.Array.Empty<ResourceDefinition>();

        public IReadOnlyList<ResourceDefinition> Resources => resources;

        public bool TryGet(string resourceId, out ResourceDefinition definition)
        {
            definition = null;
            var id = ContentId.Normalize(resourceId);
            if (resources == null || string.IsNullOrEmpty(id))
                return false;
            for (var i = 0; i < resources.Length; i++)
            {
                var entry = resources[i];
                if (entry == null || entry.ResourceId != id)
                    continue;
                definition = entry;
                return true;
            }

            return false;
        }
    }
}
