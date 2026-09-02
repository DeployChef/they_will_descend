using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumeUnassignWorkerCommandsSystem))]
    [UpdateBefore(typeof(TheyWillDescend.Simulation.Gods.ConsumeSetPyramidFeedCommandsSystem))]
    public partial struct ConsumeSetWorkplacePausedCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
        }

        public void OnUpdate(ref SystemState state) => Run(state.EntityManager);

        public static void Run(EntityManager em)
        {
            if (!SimSessionAccess.TryGet(em, out var session) || !em.HasBuffer<SetWorkplacePausedCommand>(session))
                return;

            var commands = em.GetBuffer<SetWorkplacePausedCommand>(session);
            if (commands.Length == 0)
                return;

            var copy = commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            for (var i = 0; i < copy.Length; i++)
                Apply(em, copy[i]);
            copy.Dispose();
        }

        static void Apply(EntityManager em, in SetWorkplacePausedCommand command)
        {
            if (command.BuildingId <= 0)
                return;

            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadWrite<Workplace>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            for (var i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].Id != command.BuildingId)
                    continue;
                if (em.HasComponent<Construction>(entities[i]) || em.HasComponent<Headquarters>(entities[i]))
                    break;

                var workplace = em.GetComponentData<Workplace>(entities[i]);
                workplace.Paused = command.Paused != 0 ? (byte)1 : (byte)0;
                em.SetComponentData(entities[i], workplace);
                break;
            }

            buildings.Dispose();
        }
    }
}
