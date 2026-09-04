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

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SimSessionAccess.TryGet(em, out var session)
                || !ResearchWorld.TryGetBoard(em, out var board))
                return;

            var query = SystemAPI.QueryBuilder().WithAll<SetActiveResearchRequest>().Build();
            if (query.IsEmptyIgnoreFilter)
                return;

            var hasWorkshop = !_workshops.IsEmptyIgnoreFilter;
            using var requestEntities = query.ToEntityArray(Allocator.Temp);
            using var requests = query.ToComponentDataArray<SetActiveResearchRequest>(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                ResearchRules.TryStart(em, session, requests[i].TechId, hasWorkshop);
                em.DestroyEntity(requestEntities[i]);
            }
        }
    }
}

