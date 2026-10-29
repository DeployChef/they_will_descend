using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ProcessPlaceRoadStrokeRequestsSystem))]
    public partial struct ProcessDemolishRoadRequestsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
            state.RequireForUpdate<RoadNetwork>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SimSessionAccess.TryGet(em, out var session))
                return;

            var query = SystemAPI.QueryBuilder().WithAll<DemolishRoadRequest>().Build();
            if (query.IsEmptyIgnoreFilter)
                return;

            using var requestEntities = query.ToEntityArray(Allocator.Temp);
            using var requests = query.ToComponentDataArray<DemolishRoadRequest>(Allocator.Temp);
            for (var i = 0; i < requests.Length; i++)
            {
                RoadSections.BeginDismantle(em, session, requests[i].SegmentId);
                em.DestroyEntity(requestEntities[i]);
            }
        }
    }
}
