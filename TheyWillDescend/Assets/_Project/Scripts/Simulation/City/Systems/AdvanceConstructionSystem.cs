using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.City
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct AdvanceConstructionSystem : ISystem
    {
        EntityQuery _sites;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<Construction>();
            _sites = state.GetEntityQuery(
                ComponentType.ReadWrite<Construction>(),
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        public void OnUpdate(ref SystemState state)
        {
            Run(state.EntityManager, _sites);
        }

        public static void Run(EntityManager em, EntityQuery sites)
        {
            if (!SimBridgeAccess.TryGet(em, out var session))
                return;

            var control = em.GetComponentData<SimControl>(session);
            if (!control.IsRunning)
                return;
            var dt = control.DeltaTime;
            if (dt <= 0f)
                return;

            if (!em.HasBuffer<BuildingPrototype>(session) || sites.IsEmptyIgnoreFilter)
                return;

            using var entities = sites.ToEntityArray(Allocator.Temp);
            var finished = new NativeList<Entity>(8, Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var construction = em.GetComponentData<Construction>(entity);
                construction.Elapsed += dt;
                if (construction.IsComplete)
                    finished.Add(entity);
                else
                    em.SetComponentData(entity, construction);
            }

            for (var i = 0; i < finished.Length; i++)
                FinishSite(em, session, finished[i]);
            finished.Dispose();
        }

        static void FinishSite(EntityManager em, Entity session, Entity site)
        {
            if (!em.Exists(site) || !em.HasComponent<Building>(site))
                return;

            var building = em.GetComponentData<Building>(site);
            var transform = em.HasComponent<LocalTransform>(site)
                ? em.GetComponentData<LocalTransform>(site)
                : LocalTransform.Identity;

            var catalog = em.GetBuffer<BuildingPrototype>(session);
            var prefab = ConsumePlaceBuildingCommandsSystem.ResolveHousePrefab(catalog, building.TypeId);

            em.DestroyEntity(site);
            if (prefab == Entity.Null)
                return;

            ConsumePlaceBuildingCommandsSystem.SpawnFinishedHouse(em, prefab, building, transform);
        }
    }
}
