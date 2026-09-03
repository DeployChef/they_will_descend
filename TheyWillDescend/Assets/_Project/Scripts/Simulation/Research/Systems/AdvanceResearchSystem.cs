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
                || !ResearchWorld.TryGetBoard(em, out var board))
                return;

            var capacity = ResearchRules.MeasureCapacity(_workshops);
            if (em.HasComponent<ResearchCapacity>(board))
                em.SetComponentData(board, capacity);

            var control = em.GetComponentData<SimControl>(session);
            if (!control.IsRunning)
                return;
            var dt = control.DeltaTime;
            if (dt <= 0f)
                return;

            var time = em.GetComponentData<GameTime>(session);
            if (!time.IsWorkShift)
                return;

            var research = em.GetComponentData<ResearchControl>(board);
            if (research.ActiveTechId.IsEmpty
                || !ResearchWorld.TryFindCard(em, research.ActiveTechId, out var card, out var info, out var progress))
                return;

            if (progress.IsCompleted)
            {
                research.ActiveTechId = default;
                em.SetComponentData(board, research);
                return;
            }

            var required = info.RequiredHours > 0.0001f ? info.RequiredHours : 1f;
            var load = capacity.WorkshopLoad;
            if (load <= 0f)
                return;

            var dayDuration = time.DayDuration > 0.0001f ? time.DayDuration : 1f;
            progress.AccumulatedHours += load * dt * 24f / dayDuration;
            if (progress.AccumulatedHours + 0.0001f >= required)
            {
                progress.AccumulatedHours = required;
                progress.Completed = 1;
                research.ActiveTechId = default;
                ResearchRules.ApplyEffect(em, board, info, ref research);
            }

            em.SetComponentData(card, progress);
            em.SetComponentData(board, research);
        }
    }
}
