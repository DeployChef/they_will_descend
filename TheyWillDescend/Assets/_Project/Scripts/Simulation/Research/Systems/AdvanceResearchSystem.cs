using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// Work-shift research tick. Load is the sum of finished workshop workplaces.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TheyWillDescend.Simulation.Economy.BuildingProductionSystem))]
    [UpdateBefore(typeof(TheyWillDescend.Simulation.Gods.PyramidBurnSystem))]
    public partial struct AdvanceResearchSystem : ISystem
    {
        EntityQuery _workshops;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<ResearchControl>();
            _workshops = state.GetEntityQuery(ResearchRules.FinishedWorkshopQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SimSessionAccess.TryGet(em, out var session)
                || !SimSessionAccess.TryGetResearch(em, session, out var research))
                return;

            var capacity = ResearchRules.MeasureCapacity(_workshops);
            if (em.HasComponent<ResearchCapacity>(research))
                em.SetComponentData(research, capacity);

            var control = em.GetComponentData<SimControl>(session);
            if (!control.IsRunning)
                return;
            var dt = control.DeltaTime;
            if (dt <= 0f)
                return;

            var time = em.GetComponentData<GameTime>(session);
            if (!time.IsWorkShift)
                return;

            var researchControl = em.GetComponentData<ResearchControl>(research);
            if (researchControl.ActiveTechId.IsEmpty
                || !em.HasBuffer<ResearchLine>(research)
                || !em.HasBuffer<TechInfo>(research))
                return;

            var lines = em.GetBuffer<ResearchLine>(research);
            var index = ResearchRules.IndexOf(lines, researchControl.ActiveTechId);
            if (index < 0)
                return;

            var row = lines[index];
            if (row.IsCompleted)
            {
                researchControl.ActiveTechId = default;
                em.SetComponentData(research, researchControl);
                return;
            }

            if (!ResearchRules.TryGetInfo(em.GetBuffer<TechInfo>(research), row.TechId, out var info))
                return;

            var required = info.RequiredHours > 0.0001f ? info.RequiredHours : 1f;
            var load = capacity.WorkshopLoad;
            if (load <= 0f)
                return;

            var dayDuration = time.DayDuration > 0.0001f ? time.DayDuration : 1f;
            row.AccumulatedHours += load * dt * 24f / dayDuration;
            if (row.AccumulatedHours + 0.0001f >= required)
            {
                row.AccumulatedHours = required;
                row.Completed = 1;
                researchControl.ActiveTechId = default;
                ResearchRules.ApplyEffect(em, research, info, ref researchControl);
            }

            lines[index] = row;
            em.SetComponentData(research, researchControl);
        }
    }
}
