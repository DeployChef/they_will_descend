using System.Collections.Generic;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.Economy
{
    /// <summary>
    /// Bakes known resources onto the session entity at amount 0.
    /// Starting amounts come from ScenarioDefinition.
    /// Must sit on the same GO as SimControlAuthoring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceCatalogAuthoring : MonoBehaviour
    {
        [SerializeField] ResourceCatalogAsset catalog;

        public ResourceCatalogAsset Catalog => catalog;

        class Baker : Baker<ResourceCatalogAuthoring>
        {
            public override void Bake(ResourceCatalogAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var amounts = AddBuffer<ResourceAmount>(entity);
                var info = AddBuffer<ResourceInfo>(entity);
                var so = authoring.catalog;
                if (so == null)
                {
                    Debug.LogError("ResourceCatalogAuthoring: assign a Resource Catalog asset.", authoring);
                    return;
                }

                DependsOn(so);
                var seen = new HashSet<string>(System.StringComparer.Ordinal);
                var resources = so.Resources;
                for (var i = 0; i < resources.Count; i++)
                {
                    var definition = resources[i];
                    if (definition == null)
                    {
                        Debug.LogError("Resource catalog has a missing definition.", authoring);
                        continue;
                    }

                    DependsOn(definition);
                    var id = definition.ResourceId;
                    if (string.IsNullOrEmpty(id) || !ContentId.TryEncode(id, out var key))
                    {
                        Debug.LogError($"Resource '{definition.name}' has an empty or too-long resourceId.", definition);
                        continue;
                    }

                    if (!seen.Add(id))
                    {
                        Debug.LogError($"Resource catalog: duplicate resourceId {id} ({definition.name}).", definition);
                        continue;
                    }

                    amounts.Add(new ResourceAmount { ResourceId = key, Amount = 0f });
                    info.Add(new ResourceInfo
                    {
                        ResourceId = key,
                        DisplayName = definition.DisplayName
                    });
                }
            }
        }
    }
}
