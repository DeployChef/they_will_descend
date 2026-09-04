using TheyWillDescend.Simulation.Content;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// Research lives in the simulation world as cards, not on the session bag.
    /// Populate at run start from loaded catalogs (base + later packs).
    /// </summary>
    public static class ResearchWorld
    {
        public static bool TryGetBoard(EntityManager em, out Entity board)
        {
            board = default;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ResearchControl>());
            if (query.CalculateEntityCount() != 1)
                return false;
            board = query.GetSingletonEntity();
            return true;
        }

        public static bool CommandsDrained(EntityManager em)
        {
            if (!TryGetBoard(em, out var board) || !em.HasBuffer<SetActiveResearchCommand>(board))
                return true;
            return em.GetBuffer<SetActiveResearchCommand>(board).Length == 0;
        }

        public static void DestroyAll(EntityManager em)
        {
            using var cards = em.CreateEntityQuery(ComponentType.ReadOnly<TechCard>());
            em.DestroyEntity(cards);
            using var board = em.CreateEntityQuery(ComponentType.ReadOnly<ResearchControl>());
            em.DestroyEntity(board);
        }

        /// <summary>
        /// Wipe previous cards and spawn from the catalogs currently loaded for
        /// this run. Later Addressables/plugins append more assets to the same list.
        /// Difficulty may rewrite costs on the spawned cards after this returns.
        /// </summary>
        public static int Populate(EntityManager em, TechCatalogAsset[] catalogs)
        {
            DestroyAll(em);
            var board = em.CreateEntity();
            em.AddComponentData(board, ResearchControl.Initial);
            em.AddComponentData(board, new ResearchCapacity());
            em.AddBuffer<SetActiveResearchCommand>(board);
            em.AddBuffer<UnlockedBuilding>(board);
#if UNITY_EDITOR
            em.SetName(board, "ResearchBoard");
#endif

            if (catalogs == null || catalogs.Length == 0)
            {
                Debug.LogWarning("ResearchWorld.Populate: GameSession has no tech catalogs.");
                return 0;
            }

            var spawned = 0;
            for (var i = 0; i < catalogs.Length; i++)
                spawned += SpawnCatalog(em, catalogs[i], i);
            return spawned;
        }

        public static bool TryFindCard(
            EntityManager em,
            in FixedString64Bytes techId,
            out Entity card,
            out TechInfo info,
            out ResearchProgress progress)
        {
            card = default;
            info = default;
            progress = default;
            if (techId.IsEmpty)
                return false;

            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<TechCard>(),
                ComponentType.ReadOnly<TechInfo>(),
                ComponentType.ReadOnly<ResearchProgress>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var infos = query.ToComponentDataArray<TechInfo>(Allocator.Temp);
            for (var i = 0; i < infos.Length; i++)
            {
                if (infos[i].TechId != techId)
                    continue;
                card = entities[i];
                info = infos[i];
                progress = em.GetComponentData<ResearchProgress>(card);
                return true;
            }

            return false;
        }

        static int SpawnCatalog(EntityManager em, TechCatalogAsset catalog, int slot)
        {
            if (catalog == null)
            {
                Debug.LogWarning($"ResearchWorld.Populate: tech catalog slot {slot} is empty.");
                return 0;
            }

            var specs = catalog.Techs;
            if (specs.Length == 0)
            {
                Debug.LogWarning($"ResearchWorld.Populate: '{catalog.name}' has no techs.", catalog);
                return 0;
            }

            var spawned = 0;
            for (var i = 0; i < specs.Length; i++)
            {
                if (SpawnCard(em, catalog, specs[i]))
                    spawned++;
            }

            return spawned;
        }

        static bool SpawnCard(EntityManager em, TechCatalogAsset catalog, in TechSpec spec)
        {
            var idRaw = ContentId.Normalize(spec.techId, spec.displayName);
            if (!ContentId.TryEncode(idRaw, out var techId))
            {
                Debug.LogError("Tech catalog: empty or too-long techId.", catalog);
                return false;
            }

            if (TryFindCard(em, techId, out _, out _, out _))
            {
                Debug.LogError($"Tech catalog: duplicate techId {idRaw}.", catalog);
                return false;
            }

            var name = string.IsNullOrWhiteSpace(spec.displayName) ? idRaw : spec.displayName;
            var summary = spec.summary ?? string.Empty;
            if (summary.Length > 500)
            {
                Debug.LogWarning($"Tech '{idRaw}' summary truncated to 500 chars.", catalog);
                summary = summary.Substring(0, 500);
            }

            var card = em.CreateEntity();
            em.AddComponentData(card, new TechCard());
            em.AddComponentData(card, new TechInfo
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
            em.AddComponentData(card, new ResearchProgress());
            var prereqs = em.AddBuffer<TechPrerequisite>(card);
            var requires = ContentId.Normalize(spec.requiresTechId);
            if (!string.IsNullOrEmpty(requires) && ContentId.TryEncode(requires, out var parentId))
                prereqs.Add(new TechPrerequisite { RequiresTechId = parentId });

            var costs = em.AddBuffer<TechCatalogCost>(card);
            var entries = spec.costs;
            if (entries != null)
            {
                for (var c = 0; c < entries.Length; c++)
                {
                    var entry = entries[c];
                    if (entry.Resource == null || entry.Amount <= 0.0001f)
                        continue;
                    costs.Add(new TechCatalogCost
                    {
                        ResourceId = ContentId.EncodeOrEmpty(entry.Resource.ResourceId),
                        Amount = entry.Amount
                    });
                }
            }

#if UNITY_EDITOR
            em.SetName(card, $"Tech_{idRaw}");
#endif
            return true;
        }
    }
}
