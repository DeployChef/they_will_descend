using TheyWillDescend.Simulation.Gods;
using TheyWillDescend.Simulation.Research;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Completes lifecycle transitions only after the full command pipeline drained.
    /// </summary>
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumeSetActiveResearchCommandsSystem))]
    [UpdateBefore(typeof(TheyWillDescend.Simulation.Time.ApplySimDeltaTimeSystem))]
    public partial struct FinalizeSimSessionLifecycleSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SimSessionAccess.TryGet(em, out var session)
                || !SimSessionAccess.AreLifecycleQueuesDrained(em, session))
                return;

            var lifecycle = em.GetComponentData<SimSession>(session);
            switch (lifecycle.Phase)
            {
                case SimSessionPhase.Preparing:
                    lifecycle.Phase = SimSessionPhase.Ready;
                    break;
                case SimSessionPhase.Resetting:
                    lifecycle.Phase = SimSessionPhase.Unprepared;
                    break;
                default:
                    return;
            }

            em.SetComponentData(session, lifecycle);
        }
    }
}
