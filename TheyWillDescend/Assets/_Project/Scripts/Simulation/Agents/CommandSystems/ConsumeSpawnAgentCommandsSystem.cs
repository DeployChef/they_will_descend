using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumeSimClockCommandsSystem))]
    [UpdateBefore(typeof(ConsumePlaceBuildingCommandsSystem))]
    public partial struct ConsumeSpawnAgentCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
            state.RequireForUpdate<AgentIdSequence>();
            state.RequireForUpdate<SimPrototypes>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SimSessionAccess.TryGet(em, out var session))
                return;

            var query = SystemAPI.QueryBuilder().WithAll<SpawnAgentRequest>().Build();
            if (query.IsEmptyIgnoreFilter)
                return;

            var inGame = em.GetComponentData<SimControl>(session).SessionInGame != 0;
            using var requestEntities = query.ToEntityArray(Allocator.Temp);
            using var requests = query.ToComponentDataArray<SpawnAgentRequest>(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                if (inGame)
                {
                    var req = requests[i];
                    AgentSpawn.Spawn(em, session, in req);
                }
                em.DestroyEntity(requestEntities[i]);
            }
        }
    }
}
