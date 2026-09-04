using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Gods
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateBefore(typeof(FinalizeSimSessionLifecycleSystem))]
    public partial struct ConsumeSetPyramidFeedCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
        }

        public void OnUpdate(ref SystemState state) => Run(state.EntityManager);

        public static void Run(EntityManager em)
        {
            if (!SimSessionAccess.TryGet(em, out var session) || !em.HasBuffer<SetPyramidFeedCommand>(session))
                return;

            var commands = em.GetBuffer<SetPyramidFeedCommand>(session);
            if (commands.Length == 0)
                return;

            var copy = commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            for (var i = 0; i < copy.Length; i++)
                Apply(em, session, copy[i]);
            copy.Dispose();
        }

        static void Apply(EntityManager em, Entity session, in SetPyramidFeedCommand command)
        {
            if (command.ResourceId.IsEmpty || !em.HasBuffer<ResourceInfo>(session))
                return;

            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<Headquarters>(),
                ComponentType.ReadWrite<PyramidFeedLine>());
            if (query.IsEmptyIgnoreFilter)
                return;

            var hq = query.GetSingletonEntity();

            var feed = em.GetBuffer<PyramidFeedLine>(hq);
            var info = em.GetBuffer<ResourceInfo>(session);
            PyramidFeed.SetPerHour(feed, info, command.ResourceId, command.PerHour);
        }
    }
}
