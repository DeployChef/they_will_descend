using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumeUnassignWorkerCommandsSystem))]
    [UpdateBefore(typeof(TheyWillDescend.Simulation.Agents.ConsumeSetWorkplacePausedCommandsSystem))]
    public partial struct ConsumeDeconstructBuildingCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
        }

        public void OnUpdate(ref SystemState state) => Run(state.EntityManager);

        public static void Run(EntityManager em)
        {
            if (!SimSessionAccess.TryGet(em, out var session) || !em.HasBuffer<DeconstructBuildingCommand>(session))
                return;

            var commands = em.GetBuffer<DeconstructBuildingCommand>(session);
            if (commands.Length == 0)
                return;

            var copy = commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            var lifecycle = em.GetComponentData<SimSession>(session);
            for (var i = 0; i < copy.Length; i++)
            {
                if (!lifecycle.IsReady)
                    continue;
                Apply(em, copy[i].BuildingId);
            }

            copy.Dispose();
        }

        static void Apply(EntityManager em, int buildingId)
        {
            if (buildingId <= 0 || !TryGetBuilding(em, buildingId, out var entity))
                return;
            if (em.HasComponent<Headquarters>(entity))
                return;

            ConsumeUnassignWorkerCommandsSystem.UnassignAll(em, buildingId);

            if (em.HasComponent<Construction>(entity))
            {
                var site = em.GetComponentData<Construction>(entity);
                if (site.IsDismantling)
                    return;
                site.Dismantling = 1;
                em.SetComponentData(entity, site);
                if (site.IsComplete)
                    BuildingDismantle.Complete(em, entity);
                return;
            }

            var duration = em.HasComponent<BuildingType>(entity)
                ? em.GetComponentData<BuildingType>(entity).ConstructionDuration
                : 0f;
            if (duration < 0f)
                duration = 0f;
            var construction = new Construction
            {
                Elapsed = duration,
                Duration = duration,
                Dismantling = 1
            };
            if (construction.IsComplete)
            {
                BuildingDismantle.Complete(em, entity);
                return;
            }

            em.AddComponentData(entity, construction);
#if UNITY_EDITOR
            em.SetName(entity, $"BuildingSite_{buildingId}");
#endif
        }

        static bool TryGetBuilding(EntityManager em, int buildingId, out Entity entity)
        {
            entity = Entity.Null;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            for (var i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].Id != buildingId)
                    continue;
                entity = entities[i];
                buildings.Dispose();
                return true;
            }

            buildings.Dispose();
            return false;
        }
    }
}
