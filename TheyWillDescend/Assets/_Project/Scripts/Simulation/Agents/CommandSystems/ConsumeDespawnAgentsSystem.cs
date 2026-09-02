using TheyWillDescend.Simulation.Session;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumeSimClockCommandsSystem))]
    [UpdateBefore(typeof(TheyWillDescend.Simulation.City.ConsumeDespawnBuildingsSystem))]
    public partial struct ConsumeDespawnAgentsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
            state.RequireForUpdate<AgentIdSequence>();
            state.RequireForUpdate<DespawnAllAgentsCommand>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Run(state.EntityManager);
        }

        public static void Run(EntityManager em)
        {
            if (!SimSessionAccess.TryGet(em, out var session)
                || !em.HasBuffer<DespawnAllAgentsCommand>(session))
                return;

            var commands = em.GetBuffer<DespawnAllAgentsCommand>(session);
            if (commands.Length == 0)
                return;

            commands.Clear();
            using var agents = em.CreateEntityQuery(ComponentType.ReadOnly<AgentId>());
            SimEntityDestroy.DestroyQuery(em, agents);
            em.SetComponentData(session, new AgentIdSequence());
        }
    }
}
