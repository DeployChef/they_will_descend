using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(TheyWillDescend.Simulation.Gods.ConsumeSetPyramidFeedCommandsSystem))]
    [UpdateBefore(typeof(FinalizeSimSessionLifecycleSystem))]
    public partial struct ConsumeSetActiveResearchCommandsSystem : ISystem
    {
        EntityQuery _workshops;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
            state.RequireForUpdate<ResearchControl>();
            _workshops = state.GetEntityQuery(ResearchRules.FinishedWorkshopQuery);
        }

        public void OnUpdate(ref SystemState state) => Run(state.EntityManager, _workshops);

        public static void Run(EntityManager em, EntityQuery workshops)
        {
            if (!SimSessionAccess.TryGet(em, out var session)
                || !ResearchWorld.TryGetBoard(em, out var board)
                || !em.HasBuffer<SetActiveResearchCommand>(board))
                return;

            var commands = em.GetBuffer<SetActiveResearchCommand>(board);
            if (commands.Length == 0)
                return;

            var hasWorkshop = !workshops.IsEmptyIgnoreFilter;
            var copy = commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            for (var i = 0; i < copy.Length; i++)
                ResearchRules.TryStart(em, session, copy[i].TechId, hasWorkshop);
            copy.Dispose();
        }
    }
}
