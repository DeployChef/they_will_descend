using TheyWillDescend.Simulation.Io;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    public partial struct ConsumeDespawnAgentsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimBridge>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Run(state.EntityManager);
        }

        public static void Run(EntityManager em)
        {
            if (!SimBridgeAccess.TryGet(em, out var session))
                return;

            var bridge = em.GetComponentData<SimBridge>(session);
            if (bridge.DespawnAllAgents == 0)
                return;

            using var agents = em.CreateEntityQuery(
                ComponentType.ReadOnly<AgentId>(),
                ComponentType.ReadOnly<CircleWalk>());
            SimEntityDestroy.DestroyQuery(em, agents);
            bridge.DespawnAllAgents = 0;
            em.SetComponentData(session, bridge);
        }
    }
}
