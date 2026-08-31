using System.Collections.Generic;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    /// <summary>
    /// Bakes <see cref="BuildingPrototype"/> onto the session entity.
    /// Must sit on the same GO as SimControlAuthoring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingCatalogAuthoring : MonoBehaviour
    {
        [SerializeField] BuildingCatalogAsset catalog;

        public BuildingCatalogAsset Catalog => catalog;

        public bool TryGet(string typeId, out GameObject prefab)
        {
            prefab = null;
            return catalog != null && catalog.TryGet(typeId, out prefab);
        }

        class Baker : Baker<BuildingCatalogAuthoring>
        {
            public override void Bake(BuildingCatalogAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var buffer = AddBuffer<BuildingPrototype>(entity);
                var so = authoring.catalog;
                if (so == null)
                {
                    Debug.LogError("BuildingCatalogAuthoring: assign a Building Catalog asset.", authoring);
                    return;
                }

                DependsOn(so);
                var seen = new HashSet<string>(System.StringComparer.Ordinal);
                var prefabs = so.Prefabs;
                for (var i = 0; i < prefabs.Count; i++)
                    BakeEntry(buffer, seen, prefabs[i], authoring);
            }

            void BakeEntry(
                DynamicBuffer<BuildingPrototype> buffer,
                HashSet<string> seen,
                GameObject prefab,
                BuildingCatalogAuthoring host)
            {
                if (prefab == null)
                {
                    Debug.LogError("Building catalog has a missing prefab.", host);
                    return;
                }

                DependsOn(prefab);
                var key = prefab.GetComponent<BuildingKey>();
                if (key == null)
                {
                    Debug.LogError($"Prefab '{prefab.name}' needs a BuildingKey.", prefab);
                    return;
                }

                var typeId = key.TypeId;
                if (string.IsNullOrEmpty(typeId) || !ContentId.TryEncode(typeId, out var typeKey))
                {
                    Debug.LogError($"Building '{prefab.name}' has an empty or too-long typeId.", prefab);
                    return;
                }

                if (!seen.Add(typeId))
                {
                    Debug.LogError($"Building catalog: duplicate typeId {typeId} ({prefab.name}).", prefab);
                    return;
                }

                if (prefab.GetComponent<BuildingFootprintAuthoring>() == null)
                {
                    Debug.LogError($"Prefab '{prefab.name}' needs BuildingFootprintAuthoring.", prefab);
                    return;
                }

                var stamp = GetEntity(prefab, TransformUsageFlags.Dynamic);
                if (stamp == Entity.Null)
                    return;

                buffer.Add(new BuildingPrototype
                {
                    TypeId = typeKey,
                    Prefab = stamp
                });
            }
        }
    }
}
