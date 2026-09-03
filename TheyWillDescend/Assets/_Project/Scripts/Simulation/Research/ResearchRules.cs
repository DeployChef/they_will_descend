using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    public static class ResearchRules
    {
        public static int IndexOf(DynamicBuffer<ResearchLine> lines, in FixedString64Bytes techId)
        {
            if (techId.IsEmpty)
                return -1;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].TechId == techId)
                    return i;
            }

            return -1;
        }

        public static bool TryGetInfo(
            DynamicBuffer<TechInfo> catalog,
            in FixedString64Bytes techId,
            out TechInfo info)
        {
            info = default;
            if (techId.IsEmpty || !catalog.IsCreated)
                return false;
            for (var i = 0; i < catalog.Length; i++)
            {
                if (catalog[i].TechId != techId)
                    continue;
                info = catalog[i];
                return true;
            }

            return false;
        }

        public static bool IsAvailable(
            in TechInfo info,
            in ResearchControl control,
            DynamicBuffer<ResearchLine> lines,
            DynamicBuffer<TechPrerequisite> prereqs)
        {
            if (info.TechId.IsEmpty)
                return false;
            var index = IndexOf(lines, info.TechId);
            if (index >= 0 && lines[index].IsCompleted)
                return false;
            var requiredTier = info.RequiredTier < 1 ? 1 : info.RequiredTier;
            if (requiredTier > control.UnlockedTier)
                return false;
            if (!prereqs.IsCreated)
                return true;
            for (var i = 0; i < prereqs.Length; i++)
            {
                var row = prereqs[i];
                if (row.TechId != info.TechId || row.RequiresTechId.IsEmpty)
                    continue;
                var parent = IndexOf(lines, row.RequiresTechId);
                if (parent < 0 || !lines[parent].IsCompleted)
                    return false;
            }

            return true;
        }

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

        public static bool IsBuildingUnlocked(
            EntityManager em,
            Entity session,
            in FixedString64Bytes typeId)
        {
            if (typeId.IsEmpty
                || !SimSessionAccess.TryGetResearch(em, session, out var research)
                || !em.HasBuffer<UnlockedBuilding>(research))
                return false;
            var unlocked = em.GetBuffer<UnlockedBuilding>(research);
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

        public static void ResetRun(EntityManager em, Entity session)
        {
            if (!SimSessionAccess.TryGetResearch(em, session, out var research)
                || !em.HasComponent<ResearchControl>(research)
                || !em.HasBuffer<ResearchLine>(research))
                return;

            em.SetComponentData(research, ResearchControl.Initial);
            var lines = em.GetBuffer<ResearchLine>(research);
            for (var i = 0; i < lines.Length; i++)
            {
                var row = lines[i];
                row.AccumulatedHours = 0f;
                row.Completed = 0;
                row.CostPaid = 0;
                lines[i] = row;
            }

            RebuildEffects(em, research);
        }

        public static void RebuildEffects(EntityManager em, Entity research)
        {
            if (!em.HasComponent<ResearchControl>(research)
                || !em.HasBuffer<ResearchLine>(research)
                || !em.HasBuffer<TechInfo>(research))
                return;

            var control = em.GetComponentData<ResearchControl>(research);
            control.UnlockedTier = 1;
            if (em.HasBuffer<UnlockedBuilding>(research))
                em.GetBuffer<UnlockedBuilding>(research).Clear();

            var lines = em.GetBuffer<ResearchLine>(research);
            var catalog = em.GetBuffer<TechInfo>(research);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].IsCompleted)
                    continue;
                if (!TryGetInfo(catalog, lines[i].TechId, out var info))
                    continue;
                ApplyEffect(em, research, info, ref control);
            }

            em.SetComponentData(research, control);
        }

        public static void ApplyEffect(
            EntityManager em,
            Entity research,
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
                    if (info.EffectTarget.IsEmpty || !em.HasBuffer<UnlockedBuilding>(research))
                        break;
                    var unlocked = em.GetBuffer<UnlockedBuilding>(research);
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
                || !SimSessionAccess.TryGetResearch(em, session, out var research)
                || !em.HasComponent<ResearchControl>(research)
                || !em.HasBuffer<ResearchLine>(research)
                || !em.HasBuffer<TechInfo>(research))
                return false;

            var control = em.GetComponentData<ResearchControl>(research);
            if (control.ActiveTechId == techId)
                return true;
            if (!hasWorkshop)
                return false;
            if (!TryGetInfo(em.GetBuffer<TechInfo>(research), techId, out var info))
                return false;

            var lines = em.GetBuffer<ResearchLine>(research);
            var prereqs = em.HasBuffer<TechPrerequisite>(research)
                ? em.GetBuffer<TechPrerequisite>(research)
                : default;
            if (!IsAvailable(info, control, lines, prereqs))
                return false;

            var index = IndexOf(lines, techId);
            if (index < 0)
                return false;

            var row = lines[index];
            if (!row.IsCostPaid)
            {
                if (!em.HasBuffer<TechCatalogCost>(research) || !em.HasBuffer<ResourceAmount>(session))
                    return false;
                var costs = em.GetBuffer<TechCatalogCost>(research);
                var stock = em.GetBuffer<ResourceAmount>(session);
                if (!TechCosts.CanAfford(costs, techId, stock))
                    return false;
                TechCosts.Pay(costs, techId, stock);
                row.CostPaid = 1;
                lines[index] = row;
            }

            control.ActiveTechId = techId;
            em.SetComponentData(research, control);
            return true;
        }
    }
}
