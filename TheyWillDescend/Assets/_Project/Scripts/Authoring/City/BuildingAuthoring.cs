using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.City;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    /// <summary>
    /// Put on a house prefab. Numbers come from <see cref="BuildingDefinition"/> — not duplicated here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingAuthoring : MonoBehaviour
    {
        [SerializeField] BuildingDefinition definition;

        public BuildingDefinition Definition => definition;

        class Baker : Baker<BuildingAuthoring>
        {
            public override void Bake(BuildingAuthoring authoring)
            {
                var so = authoring.definition;
                if (so == null)
                {
                    Debug.LogError($"{authoring.name}: BuildingAuthoring needs a BuildingDefinition.", authoring);
                    return;
                }

                DependsOn(so);
                DependsOnRates(so.RecipeInputs);
                DependsOnRates(so.RecipeOutputs);
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new BuildingType
                {
                    TypeId = so.TypeId,
                    WidthClusters = so.WidthClusters,
                    DepthRadialRings = so.DepthRadialRings,
                    ConstructionDuration = so.ConstructionDuration,
                    WorkplaceSlots = so.WorkplaceSlots
                });
                if (so.WorkplaceSlots > 0)
                    AddComponent<Workplace>(entity);
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
