using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Gods;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.Session
{
    /// <summary>
    /// Bakes era catalog, loyalty, and pyramid feed commands onto the session.
    /// Same GO as SimControlAuthoring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimelineCatalogAuthoring : MonoBehaviour
    {
        [SerializeField] TimelineCatalogAsset catalog;

        public TimelineCatalogAsset Catalog => catalog;

        class Baker : Baker<TimelineCatalogAuthoring>
        {
            public override void Bake(TimelineCatalogAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddBuffer<SetPyramidFeedCommand>(entity);
                var eras = AddBuffer<EraLine>(entity);
                var tribute = AddBuffer<EraTributeLine>(entity);
                AddComponent(entity, GodLoyalty.Full());

                var so = authoring.catalog;
                if (so == null)
                {
                    Debug.LogError("TimelineCatalogAuthoring: assign a Timeline Catalog asset.", authoring);
                    AddComponent(entity, new Timeline
                    {
                        PreviousMaxLoyalty = 100f,
                        TargetMaxLoyalty = 100f
                    });
                    return;
                }

                DependsOn(so);
                var specs = so.Eras;
                var firstMax = 100f;
                for (var i = 0; i < specs.Length; i++)
                {
                    var spec = specs[i];
                    DependsOnEra(spec);
                    if (!ContentId.TryEncode(ContentId.Normalize(spec.eraId, spec.displayName), out var id))
                    {
                        Debug.LogError($"Timeline era {i} has an empty or too-long id.", so);
                        continue;
                    }

                    var days = spec.durationDays > 0 ? spec.durationDays : 1;
                    var max = spec.maxLoyalty;
                    if (max < 0f)
                        max = 0f;
                    else if (max > 100f)
                        max = 100f;
                    if (eras.Length == 0)
                        firstMax = max;

                    var name = string.IsNullOrWhiteSpace(spec.displayName) ? spec.eraId : spec.displayName;
                    eras.Add(new EraLine
                    {
                        EraId = id,
                        DisplayName = name,
                        DurationDays = days,
                        MaxLoyalty = max,
                        TributeEnergyMul = spec.tributeEnergyMultiplier > 0.0001f
                            ? spec.tributeEnergyMultiplier
                            : 1f,
                        LoyaltyPerEnergy = spec.loyaltyPerEnergy < 0f ? 0f : spec.loyaltyPerEnergy
                    });

                    var eraIndex = eras.Length - 1;
                    var gifts = spec.tribute;
                    if (gifts == null)
                        continue;
                    for (var t = 0; t < gifts.Length; t++)
                    {
                        var resource = gifts[t];
                        if (resource == null)
                            continue;
                        DependsOn(resource);
                        if (!ContentId.TryEncode(resource.ResourceId, out var resourceId))
                            continue;
                        tribute.Add(new EraTributeLine
                        {
                            EraIndex = eraIndex,
                            ResourceId = resourceId
                        });
                    }
                }

                if (eras.Length == 0)
                {
                    Debug.LogError("Timeline catalog is empty.", so);
                    firstMax = 100f;
                }

                AddComponent(entity, new Timeline
                {
                    EraIndex = 0,
                    PreviousMaxLoyalty = firstMax,
                    TargetMaxLoyalty = firstMax
                });
                var loyalty = GodLoyalty.Full();
                loyalty.Value = firstMax;
                loyalty.EffectiveMax = firstMax;
                SetComponent(entity, loyalty);
            }

            void DependsOnEra(in EraSpec spec)
            {
                var gifts = spec.tribute;
                if (gifts == null)
                    return;
                for (var i = 0; i < gifts.Length; i++)
                {
                    if (gifts[i] != null)
                        DependsOn(gifts[i]);
                }
            }
        }
    }
}
