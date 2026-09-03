using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Research;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.Session
{
    /// <summary>
    /// Bakes research onto a sibling entity. Session only stores
    /// <see cref="ResearchLink"/> — the SimControl archetype is full.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TechCatalogAuthoring : MonoBehaviour
    {
        [SerializeField] TechCatalogAsset catalog;

        public TechCatalogAsset Catalog => catalog;

        class Baker : Baker<TechCatalogAuthoring>
        {
            public override void Bake(TechCatalogAuthoring authoring)
            {
                var session = GetEntity(TransformUsageFlags.None);
                var research = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(session, new ResearchLink { Entity = research });
                AddComponent(research, ResearchControl.Initial);
                AddComponent(research, new ResearchCapacity());
                AddBuffer<SetActiveResearchCommand>(research);
                AddBuffer<UnlockedBuilding>(research);
                var infos = AddBuffer<TechInfo>(research);
                var prereqs = AddBuffer<TechPrerequisite>(research);
                var costs = AddBuffer<TechCatalogCost>(research);
                var lines = AddBuffer<ResearchLine>(research);

                var so = authoring.catalog;
                if (so == null)
                {
                    Debug.LogError("TechCatalogAuthoring: assign a Tech Catalog asset.", authoring);
                    return;
                }

                DependsOn(so);
                var specs = so.Techs;
                for (var i = 0; i < specs.Length; i++)
                    BakeSpec(infos, prereqs, costs, lines, specs[i], so);
            }

            void BakeSpec(
                DynamicBuffer<TechInfo> infos,
                DynamicBuffer<TechPrerequisite> prereqs,
                DynamicBuffer<TechCatalogCost> costs,
                DynamicBuffer<ResearchLine> lines,
                in TechSpec spec,
                TechCatalogAsset so)
            {
                var idRaw = ContentId.Normalize(spec.techId, spec.displayName);
                if (!ContentId.TryEncode(idRaw, out var techId))
                {
                    Debug.LogError("Tech catalog: empty or too-long techId at index.", so);
                    return;
                }

                for (var i = 0; i < infos.Length; i++)
                {
                    if (infos[i].TechId != techId)
                        continue;
                    Debug.LogError($"Tech catalog: duplicate techId {idRaw}.", so);
                    return;
                }

                var name = string.IsNullOrWhiteSpace(spec.displayName) ? idRaw : spec.displayName;
                var summary = spec.summary ?? string.Empty;
                if (summary.Length > 500)
                {
                    Debug.LogWarning($"Tech '{idRaw}' summary truncated to 500 chars.", so);
                    summary = summary.Substring(0, 500);
                }

                DependsOnCosts(spec.costs);
                infos.Add(new TechInfo
                {
                    TechId = techId,
                    DisplayName = name,
                    Summary = summary,
                    RequiredHours = spec.requiredHours > 0.0001f ? spec.requiredHours : 1f,
                    RequiredTier = spec.requiredTier < 1 ? 1 : spec.requiredTier,
                    TreeColumn = spec.treeColumn < 0 ? 0 : spec.treeColumn,
                    TreeRow = spec.treeRow < 0 ? 0 : spec.treeRow,
                    EffectKind = spec.effect,
                    EffectTarget = ContentId.EncodeOrEmpty(spec.effectTarget),
                    EffectTier = spec.effectTier
                });
                lines.Add(new ResearchLine { TechId = techId });

                var requires = ContentId.Normalize(spec.requiresTechId);
                if (!string.IsNullOrEmpty(requires) && ContentId.TryEncode(requires, out var parentId))
                {
                    prereqs.Add(new TechPrerequisite
                    {
                        TechId = techId,
                        RequiresTechId = parentId
                    });
                }

                var entries = spec.costs;
                if (entries == null)
                    return;
                for (var c = 0; c < entries.Length; c++)
                {
                    var entry = entries[c];
                    if (entry.Resource == null || entry.Amount <= 0.0001f)
                        continue;
                    DependsOn(entry.Resource);
                    costs.Add(new TechCatalogCost
                    {
                        TechId = techId,
                        ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                        Amount = entry.Amount
                    });
                }
            }

            void DependsOnCosts(BuildingCostEntry[] entries)
            {
                if (entries == null)
                    return;
                for (var i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Resource != null)
                        DependsOn(entries[i].Resource);
                }
            }
        }
    }
}
