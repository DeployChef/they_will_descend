using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    /// <summary>
    /// Bakes optional stamp modules onto the house prefab entity.
    /// <see cref="BuildingKey"/> is required; other authoring on the same GO is optional.
    /// </summary>
    public sealed class BuildingKeyBaker : Baker<BuildingKey>
    {
        public override void Bake(BuildingKey authoring)
        {
            var typeId = authoring.TypeId;
            if (string.IsNullOrEmpty(typeId) || !ContentId.TryEncode(typeId, out var typeKey))
            {
                Debug.LogError($"{authoring.name}: BuildingKey has an empty or too-long typeId.", authoring);
                return;
            }

            var footprint = GetComponent<BuildingFootprintAuthoring>();
            if (footprint == null)
            {
                Debug.LogError($"{authoring.name}: add BuildingFootprintAuthoring.", authoring);
                return;
            }

            var construction = GetComponent<BuildingConstructionAuthoring>();
            var workplace = GetComponent<BuildingWorkplaceAuthoring>();
            var recipe = GetComponent<BuildingRecipeAuthoring>();
            var cost = GetComponent<BuildingCostAuthoring>();
            DependsOnRates(recipe != null ? recipe.Inputs : null);
            DependsOnRates(recipe != null ? recipe.Outputs : null);
            DependsOnCosts(cost != null ? cost.Costs : null);

            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var slots = workplace != null ? workplace.Slots : 0;
            AddComponent(entity, new BuildingType
            {
                TypeId = typeKey,
                WidthClusters = footprint.WidthClusters,
                DepthRadialRings = footprint.DepthRadialRings,
                ConstructionDuration = construction != null ? construction.Duration : 0f,
                WorkplaceSlots = slots
            });
            AddComponent(entity, new BuildingMeshSize
            {
                Horizontal = BuildingPrefabMetrics.HorizontalSize(authoring.gameObject)
            });
            AddComponent(entity, new URPMaterialPropertyBaseColor
            {
                Value = new float4(0.55f, 0.55f, 0.58f, 1f)
            });
            if (workplace != null && slots > 0)
                AddComponent<Workplace>(entity);

            if (recipe != null)
            {
                var buffer = AddBuffer<BuildingRecipeLine>(entity);
                AddRecipe(buffer, recipe.Inputs, BuildingRecipeKind.Input);
                AddRecipe(buffer, recipe.Outputs, BuildingRecipeKind.Output);
            }

            if (cost != null)
            {
                var buffer = AddBuffer<BuildingCost>(entity);
                var list = cost.Costs;
                for (var i = 0; i < list.Length; i++)
                {
                    var entry = list[i];
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
