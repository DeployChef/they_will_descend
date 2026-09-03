using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    public static class ResearchRules
    {
        public static EntityQueryDesc FinishedWorkshopQuery => new()
        {
            All = new[]
            {
                ComponentType.ReadOnly<ResearchWorkplace>(),
                ComponentType.ReadOnly<Workplace>(),
                ComponentType.ReadOnly<BuildingType>()
            },
            None = new[]
            {
                ComponentType.ReadOnly<Construction>(),
                ComponentType.ReadOnly<Headquarters>()
            }
        };

        public static bool IsAvailable(
            EntityManager em,
            in TechInfo info,
            in ResearchProgress progress,
            in ResearchControl control)
        {
            if (info.TechId.IsEmpty || progress.IsCompleted)
                return false;
            var requiredTier = info.RequiredTier < 1 ? 1 : info.RequiredTier;
            if (requiredTier > control.UnlockedTier)
                return false;
            return ParentsCompleted(em, info.TechId);
        }

        public static bool IsBuildingUnlocked(EntityManager em, in FixedString64Bytes typeId)
        {
            if (typeId.IsEmpty
                || !ResearchWorld.TryGetBoard(em, out var board)
                || !em.HasBuffer<UnlockedBuilding>(board))
                return false;
            var unlocked = em.GetBuffer<UnlockedBuilding>(board);
            for (var i = 0; i < unlocked.Length; i++)
            {
                if (unlocked[i].TypeId == typeId)
                    return true;
            }

            return false;
        }

        public static float WorkshopLoad(EntityQuery workshops)
        {
            if (workshops.IsEmptyIgnoreFilter)
                return 0f;

            using var workplaces = workshops.ToComponentDataArray<Workplace>(Allocator.Temp);
            using var types = workshops.ToComponentDataArray<BuildingType>(Allocator.Temp);
            var load = 0f;
            for (var i = 0; i < workplaces.Length; i++)
            {
                if (workplaces[i].IsPaused)
                    continue;
                load += Workplace.Load01(workplaces[i].WorkingCount, types[i].WorkplaceSlots);
            }

            return load;
        }

        public static ResearchCapacity MeasureCapacity(EntityQuery workshops)
        {
            var load = WorkshopLoad(workshops);
            return new ResearchCapacity
            {
                WorkshopLoad = load,
                HasFinishedWorkshop = workshops.IsEmptyIgnoreFilter ? (byte)0 : (byte)1
            };
        }

        public static void RebuildEffects(EntityManager em)
        {
            if (!ResearchWorld.TryGetBoard(em, out var board)
                || !em.HasComponent<ResearchControl>(board))
                return;

            var control = em.GetComponentData<ResearchControl>(board);
            control.UnlockedTier = 1;
            if (em.HasBuffer<UnlockedBuilding>(board))
                em.GetBuffer<UnlockedBuilding>(board).Clear();

            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<TechInfo>(),
                ComponentType.ReadOnly<ResearchProgress>());
            using var infos = query.ToComponentDataArray<TechInfo>(Allocator.Temp);
            using var progress = query.ToComponentDataArray<ResearchProgress>(Allocator.Temp);
            for (var i = 0; i < infos.Length; i++)
            {
                if (!progress[i].IsCompleted)
                    continue;
                ApplyEffect(em, board, infos[i], ref control);
            }

            em.SetComponentData(board, control);
        }

        public static void ApplyEffect(
            EntityManager em,
            Entity board,
            in TechInfo info,
            ref ResearchControl control)
        {
            switch (info.EffectKind)
            {
                case TechEffectKind.RaiseResearchTier:
                    var tier = info.EffectTier < 1 ? 1 : info.EffectTier;
                    if (tier > control.UnlockedTier)
                        control.UnlockedTier = tier;
                    break;
                case TechEffectKind.UnlockBuilding:
                    if (info.EffectTarget.IsEmpty || !em.HasBuffer<UnlockedBuilding>(board))
                        break;
                    var unlocked = em.GetBuffer<UnlockedBuilding>(board);
                    var already = false;
                    for (var i = 0; i < unlocked.Length; i++)
                    {
                        if (unlocked[i].TypeId != info.EffectTarget)
                            continue;
                        already = true;
                        break;
                    }

                    if (!already)
                        unlocked.Add(new UnlockedBuilding { TypeId = info.EffectTarget });
                    break;
            }
        }

        public static bool TryStart(
            EntityManager em,
            Entity session,
            in FixedString64Bytes techId,
            bool hasWorkshop)
        {
            if (techId.IsEmpty
                || !ResearchWorld.TryGetBoard(em, out var board)
                || !ResearchWorld.TryFindCard(em, techId, out var card, out var info, out var progress))
                return false;

            var control = em.GetComponentData<ResearchControl>(board);
            if (control.ActiveTechId == techId)
                return true;
            if (!hasWorkshop)
                return false;
            if (!IsAvailable(em, info, progress, control))
                return false;

            if (!progress.IsCostPaid)
            {
                if (!em.HasBuffer<TechCatalogCost>(card) || !em.HasBuffer<ResourceAmount>(session))
                    return false;
                var costs = em.GetBuffer<TechCatalogCost>(card);
                var stock = em.GetBuffer<ResourceAmount>(session);
                if (!TechCosts.CanAfford(costs, stock))
                    return false;
                TechCosts.Pay(costs, stock);
                progress.CostPaid = 1;
                em.SetComponentData(card, progress);
            }

            control.ActiveTechId = techId;
            em.SetComponentData(board, control);
            return true;
        }

        static bool ParentsCompleted(EntityManager em, in FixedString64Bytes techId)
        {
            if (!ResearchWorld.TryFindCard(em, techId, out var card, out _, out _))
                return false;
            if (!em.HasBuffer<TechPrerequisite>(card))
                return true;
            var prereqs = em.GetBuffer<TechPrerequisite>(card);
            for (var i = 0; i < prereqs.Length; i++)
            {
                var required = prereqs[i].RequiresTechId;
                if (required.IsEmpty)
                    continue;
                if (!ResearchWorld.TryFindCard(em, required, out _, out _, out var parent)
                    || !parent.IsCompleted)
                    return false;
            }

            return true;
        }
    }
}
