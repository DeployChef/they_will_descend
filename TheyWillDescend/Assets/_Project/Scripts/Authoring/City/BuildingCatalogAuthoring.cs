using System.Collections.Generic;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using Unity.Collections;
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

        public bool TryGet(string typeId, out BuildingDefinition definition)
        {
            definition = null;
            return catalog != null && catalog.TryGet(typeId, out definition);
        }

        class Baker : Baker<BuildingCatalogAuthoring>
        {
            public override void Bake(BuildingCatalogAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var buffer = AddBuffer<BuildingPrototype>(entity);
                var costs = AddBuffer<BuildingCost>(entity);
                var recipes = AddBuffer<BuildingRecipeLine>(entity);
                var so = authoring.catalog;
                if (so == null)
                {
                    Debug.LogError("BuildingCatalogAuthoring: assign a Building Catalog asset.", authoring);
                    return;
                }

                DependsOn(so);
                var seen = new HashSet<string>(System.StringComparer.Ordinal);
                var buildings = so.Buildings;
                for (var i = 0; i < buildings.Count; i++)
                    BakeEntry(buffer, costs, recipes, seen, buildings[i], authoring);
            }

            void BakeEntry(
                DynamicBuffer<BuildingPrototype> buffer,
                DynamicBuffer<BuildingCost> costs,
                DynamicBuffer<BuildingRecipeLine> recipes,
                HashSet<string> seen,
                BuildingDefinition definition,
                BuildingCatalogAuthoring host)
            {
                if (definition == null)
                {
                    Debug.LogError("Building catalog has a missing definition.", host);
                    return;
                }

                DependsOn(definition);
                DependsOnRates(definition.RecipeInputs);
                DependsOnRates(definition.RecipeOutputs);
                var typeId = definition.TypeId;
                if (string.IsNullOrEmpty(typeId) || !ContentId.TryEncode(typeId, out var typeKey))
                {
                    Debug.LogError($"Building '{definition.name}' has an empty or too-long typeId.", definition);
                    return;
                }

                if (!seen.Add(typeId))
                {
                    Debug.LogError($"Building catalog: duplicate typeId {typeId} ({definition.name}).", definition);
                    return;
                }

                var prefab = definition.Prefab;
                if (prefab == null)
                {
                    Debug.LogError($"Building '{definition.DisplayName}' (type {typeId}) has no prefab.", definition);
                    return;
                }

                DependsOn(prefab);
                var stamp = GetEntity(prefab, TransformUsageFlags.Dynamic);
                if (stamp == Entity.Null)
                    return;

                var authoring = prefab.GetComponent<BuildingAuthoring>();
                if (authoring == null || authoring.Definition != definition)
                {
                    Debug.LogError(
                        $"Prefab '{prefab.name}' must have BuildingAuthoring pointing at '{definition.name}'.",
                        prefab);
                    return;
                }

                buffer.Add(new BuildingPrototype
                {
                    TypeId = typeKey,
                    Prefab = stamp,
                    DisplayName = definition.DisplayName,
                    WidthClusters = definition.WidthClusters,
                    DepthRadialRings = definition.DepthRadialRings,
                    MeshSize = BuildingPrefabMetrics.HorizontalSize(prefab),
                    ConstructionDuration = definition.ConstructionDuration,
                    WorkplaceSlots = definition.WorkplaceSlots
                });

                var costList = definition.BuildCost;
                for (var c = 0; c < costList.Length; c++)
                {
                    var entry = costList[c];
                    if (entry.Resource == null || entry.Amount <= 0.0001f)
                        continue;
                    DependsOn(entry.Resource);
                    costs.Add(new BuildingCost
                    {
                        TypeId = typeKey,
                        ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                        Amount = entry.Amount
                    });
                }

                BakeRecipe(recipes, typeKey, definition.RecipeInputs, BuildingRecipeKind.Input);
                BakeRecipe(recipes, typeKey, definition.RecipeOutputs, BuildingRecipeKind.Output);
            }

            void BakeRecipe(
                DynamicBuffer<BuildingRecipeLine> recipes,
                FixedString64Bytes typeKey,
                ResourceRate[] rates,
                BuildingRecipeKind kind)
            {
                if (rates == null)
                    return;
                for (var i = 0; i < rates.Length; i++)
                {
                    var entry = rates[i];
                    if (entry.Resource == null || entry.PerHour <= 0.0001f)
                        continue;
                    recipes.Add(new BuildingRecipeLine
                    {
                        TypeId = typeKey,
                        Kind = kind,
                        ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                        PerHour = entry.PerHour
                    });
                }
            }

            void DependsOnRates(ResourceRate[] rates)
            {
                if (rates == null)
                    return;
                for (var i = 0; i < rates.Length; i++)
                {
                    if (rates[i].Resource != null)
                        DependsOn(rates[i].Resource);
                }
            }
        }
    }
}
