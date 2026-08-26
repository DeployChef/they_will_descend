using TheyWillDescend.Simulation.Io;
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
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<Construction>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Run(state.EntityManager);
        }

        public static void Run(EntityManager em)
        {
            if (!SimBridgeAccess.TryGet(em, out var session))
                return;

            var control = em.GetComponentData<SimControl>(session);
            if (!control.IsRunning)
                return;
            var dt = control.DeltaTime;
            if (dt <= 0f)
                return;

            if (!em.HasBuffer<BuildingPrototype>(session))
                return;

            var catalog = em.GetBuffer<BuildingPrototype>(session);

            using var query = em.CreateEntityQuery(
                ComponentType.ReadWrite<Construction>(),
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<LocalTransform>());
            if (query.IsEmptyIgnoreFilter)
                return;

            using var entities = query.ToEntityArray(Allocator.Temp);
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
                FinishSite(em, catalog, finished[i]);
            finished.Dispose();
        }

        static void FinishSite(EntityManager em, DynamicBuffer<BuildingPrototype> catalog, Entity site)
        {
            if (!em.Exists(site) || !em.HasComponent<Building>(site))
                return;

            var building = em.GetComponentData<Building>(site);
            var transform = em.HasComponent<LocalTransform>(site)
                ? em.GetComponentData<LocalTransform>(site)
                : LocalTransform.Identity;
            var prefab = ConsumePlaceBuildingCommandsSystem.ResolveHousePrefab(
                catalog, building.TypeId, building.WidthClusters, building.DepthRadialRings);
            em.DestroyEntity(site);
            if (prefab == Entity.Null)
                return;

            ConsumePlaceBuildingCommandsSystem.SpawnFinishedHouse(em, prefab, building, transform);
        }
    }
}
