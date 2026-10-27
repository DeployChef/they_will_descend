using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Gods
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(TheyWillDescend.Simulation.City.ProcessDemolishBuildingRequestsSystem))]
    public partial struct ProcessSetPyramidFeedRequestsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SimSessionAccess.TryGet(em, out var session))
                return;

            var query = SystemAPI.QueryBuilder().WithAll<SetPyramidFeedRequest>().Build();
            if (query.IsEmptyIgnoreFilter)
                return;

            if (em.GetComponentData<SimControl>(session).SessionInGame == 0)
            {
                using var leftover = query.ToEntityArray(Allocator.Temp);
                em.DestroyEntity(leftover);
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<Headquarters>(out var hq))
                return;

            if (!em.HasBuffer<PyramidFeedLine>(hq) || !em.HasBuffer<ResourceInfo>(session))
                return;

            var feed = em.GetBuffer<PyramidFeedLine>(hq);
            var info = em.GetBuffer<ResourceInfo>(session);

            using var requestEntities = query.ToEntityArray(Allocator.Temp);
            using var requests = query.ToComponentDataArray<SetPyramidFeedRequest>(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                var req = requests[i];
                if (!req.ResourceId.IsEmpty)
                {
                    PyramidFeed.SetPerHour(feed, info, req.ResourceId, req.PerHour);
                }
            }

            // Structural changes invalidate feed/info; destroy requests after the batch.
            em.DestroyEntity(requestEntities);
        }
    }
}

