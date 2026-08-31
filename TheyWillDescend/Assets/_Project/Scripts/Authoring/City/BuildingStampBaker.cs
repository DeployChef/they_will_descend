using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    /// <summary>
    /// Bakes optional stamp packs onto the house prefab entity.
    /// </summary>
    public sealed class BuildingStampBaker : Baker<BuildingStamp>
    {
        public override void Bake(BuildingStamp authoring)
        {
            var typeId = authoring.TypeId;
            if (string.IsNullOrEmpty(typeId) || !ContentId.TryEncode(typeId, out var typeKey))
            {
                Debug.LogError($"{authoring.name}: BuildingStamp has an empty or too-long typeId.", authoring);
                return;
            }

            DependsOnCosts(authoring.Costs);
            DependsOnRates(authoring.RecipeInputs);
            DependsOnRates(authoring.RecipeOutputs);

            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var slots = authoring.WorkplaceSlots;
            AddComponent(entity, new BuildingType
            {
                TypeId = typeKey,
                WidthClusters = authoring.WidthClusters,
                DepthRadialRings = authoring.DepthRadialRings,
                ConstructionDuration = authoring.ConstructionDuration,
                WorkplaceSlots = slots
            });
            AddComponent(entity, new BuildingMeshSize
            {
                Horizontal = BuildingPrefabMetrics.HorizontalSize(authoring.gameObject)
            });
            AddBodyColors(authoring);
            if (authoring.HasWorkplace)
                AddComponent<Workplace>(entity);

            if (authoring.HasRecipe)
            {
                var buffer = AddBuffer<BuildingRecipeLine>(entity);
                AddRecipe(buffer, authoring.RecipeInputs, BuildingRecipeKind.Input);
                AddRecipe(buffer, authoring.RecipeOutputs, BuildingRecipeKind.Output);
            }

            var costs = authoring.Costs;
            if (HasAnyCost(costs))
            {
                var buffer = AddBuffer<BuildingCost>(entity);
                for (var i = 0; i < costs.Length; i++)
                {
                    var entry = costs[i];
                    if (entry.Resource == null || entry.Amount <= 0.0001f)
                        continue;
                    DependsOn(entry.Resource);
                    buffer.Add(new BuildingCost
                    {
                        ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                        Amount = entry.Amount
                    });
                }
            }
        }

        void AddRecipe(
            DynamicBuffer<BuildingRecipeLine> buffer,
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
                buffer.Add(new BuildingRecipeLine
                {
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

        void AddBodyColors(BuildingStamp authoring)
        {
            var renderers = authoring.GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.GetComponentInParent<BakeStripAuthoring>() != null)
                    continue;

                var visual = GetEntity(renderer.gameObject, TransformUsageFlags.Dynamic);
                AddComponent(visual, new URPMaterialPropertyBaseColor
                {
                    Value = new float4(0.55f, 0.55f, 0.58f, 1f)
                });
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

        static bool HasAnyCost(BuildingCostEntry[] costs)
        {
            if (costs == null)
                return false;
            for (var i = 0; i < costs.Length; i++)
            {
                if (costs[i].Resource != null && costs[i].Amount > 0.0001f)
                    return true;
            }

            return false;
        }
    }
}
