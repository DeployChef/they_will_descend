using System.Collections.Generic;
using TheyWillDescend.Content;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    /// <summary>
    /// Copies catalog stamp numbers onto the session entity. Does not convert
    /// house prefabs into ECS (art stays a Unity prefab for the view board).
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
                var basePrototypes = AddBuffer<BaseBuildingPrototype>(entity);
                var baseCosts = AddBuffer<BaseBuildingCatalogCost>(entity);
                var baseRecipes = AddBuffer<BaseBuildingCatalogRecipe>(entity);
                var prototypes = AddBuffer<BuildingPrototype>(entity);
                var costs = AddBuffer<BuildingCatalogCost>(entity);
                var recipes = AddBuffer<BuildingCatalogRecipe>(entity);
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
                    BakeEntry(
                        basePrototypes,
                        baseCosts,
                        baseRecipes,
                        prototypes,
                        costs,
                        recipes,
                        seen,
                        prefabs[i],
                        authoring);
            }

            void BakeEntry(
                DynamicBuffer<BaseBuildingPrototype> basePrototypes,
                DynamicBuffer<BaseBuildingCatalogCost> baseCosts,
                DynamicBuffer<BaseBuildingCatalogRecipe> baseRecipes,
                DynamicBuffer<BuildingPrototype> prototypes,
                DynamicBuffer<BuildingCatalogCost> costs,
                DynamicBuffer<BuildingCatalogRecipe> recipes,
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
                var stamp = prefab.GetComponent<BuildingStamp>();
                if (stamp == null)
                {
                    Debug.LogError($"Prefab '{prefab.name}' needs a BuildingStamp.", prefab);
                    return;
                }

                var typeId = stamp.TypeId;
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

                if (!stamp.Footprint.IsValid)
                {
                    Debug.LogError($"Prefab '{prefab.name}' has an invalid footprint.", prefab);
                    return;
                }

                DependsOnCosts(stamp.Costs);
                DependsOnRates(stamp.RecipeInputs);
                DependsOnRates(stamp.RecipeOutputs);

                var basePrototype = new BaseBuildingPrototype
                {
                    TypeId = typeKey,
                    WidthClusters = stamp.WidthClusters,
                    DepthRadialRings = stamp.DepthRadialRings,
                    ConstructionDuration = stamp.ConstructionDuration,
                    ConstructionCrewSlots = stamp.ConstructionCrewSlots,
                    WorkplaceSlots = stamp.WorkplaceSlots,
                    ResearchWorkplace = stamp.IsResearchWorkplace ? (byte)1 : (byte)0,
                    RequiresUnlock = stamp.RequiresUnlock ? (byte)1 : (byte)0
                };
                basePrototypes.Add(basePrototype);
                prototypes.Add(basePrototype.ToResolved());

                AddCosts(baseCosts, costs, typeKey, stamp.Costs);
                if (stamp.HasRecipe)
                {
                    AddRecipe(baseRecipes, recipes, typeKey, stamp.RecipeInputs, BuildingRecipeKind.Input);
                    AddRecipe(baseRecipes, recipes, typeKey, stamp.RecipeOutputs, BuildingRecipeKind.Output);
                }
            }

            void AddCosts(
                DynamicBuffer<BaseBuildingCatalogCost> baseBuffer,
                DynamicBuffer<BuildingCatalogCost> buffer,
                in Unity.Collections.FixedString64Bytes typeKey,
                BuildingCostEntry[] entries)
            {
                if (entries == null)
                    return;
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (entry.Resource == null || entry.Amount <= 0.0001f)
                        continue;
                    DependsOn(entry.Resource);
                    var row = new BaseBuildingCatalogCost
                    {
                        TypeId = typeKey,
                        ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                        Amount = entry.Amount
                    };
                    baseBuffer.Add(row);
                    buffer.Add(row.ToResolved());
                }
            }

            void AddRecipe(
                DynamicBuffer<BaseBuildingCatalogRecipe> baseBuffer,
                DynamicBuffer<BuildingCatalogRecipe> buffer,
                in Unity.Collections.FixedString64Bytes typeKey,
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
                    DependsOn(entry.Resource);
                    var row = new BaseBuildingCatalogRecipe
                    {
                        TypeId = typeKey,
                        Kind = kind,
                        ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                        PerHour = entry.PerHour
                    };
                    baseBuffer.Add(row);
                    buffer.Add(row.ToResolved());
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

            void DependsOnCosts(BuildingCostEntry[] costs)
            {
                if (costs == null)
                    return;
                for (var i = 0; i < costs.Length; i++)
                {
                    if (costs[i].Resource != null)
                        DependsOn(costs[i].Resource);
                }
            }
        }
    }
}
