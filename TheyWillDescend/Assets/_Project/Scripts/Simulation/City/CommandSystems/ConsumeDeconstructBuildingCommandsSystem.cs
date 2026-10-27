using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumePlaceBuildingCommandsSystem))]
    public partial struct ConsumeDeconstructBuildingCommandsSystem : ISystem
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

            var query = SystemAPI.QueryBuilder().WithAll<DemolishBuildingRequest>().Build();
            if (query.IsEmptyIgnoreFilter)
                return;

            var inGame = em.GetComponentData<SimControl>(session).SessionInGame != 0;
            using var requestEntities = query.ToEntityArray(Allocator.Temp);
            using var requests = query.ToComponentDataArray<DemolishBuildingRequest>(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                if (inGame)
                    BuildingDismantle.Begin(em, requests[i].BuildingId);
                em.DestroyEntity(requestEntities[i]);
            }
        }
    }
}
