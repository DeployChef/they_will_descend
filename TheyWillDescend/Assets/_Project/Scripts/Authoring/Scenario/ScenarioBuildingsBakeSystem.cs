using TheyWillDescend.Authoring.City;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Authoring.Scenario
{
    /// <summary>
    /// Copies SubScene scenario houses/stock/worker count onto the session
    /// at bake time (editor seed). Play overwrites the same buffers in
    /// <c>RunPublisher</c> from the menu kit — this system does not run in Play.
    /// Runs after HQ writes CityGrid.Center so poses sit on the plaza origin.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    public partial struct ScenarioBuildingsBakeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CityGrid>(out var session))
                return;

            var em = state.EntityManager;
            ApplyStartingStock(em, session);
            WritePendingPlaces(em, session);
            WritePendingWorkers(em, session);
        }

        static void ApplyStartingStock(EntityManager em, Entity session)
        {
            using var specQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ScenarioResourceSpec>());
            if (specQuery.IsEmptyIgnoreFilter)
                return;
            if (!em.HasBuffer<ResourceAmount>(session) || !em.HasBuffer<ResourceInfo>(session))
            {
                Debug.LogError("Scenario bake: session has no resource ledger. Add ResourceCatalogAuthoring on SimControl.");
                return;
            }

            var stock = em.GetBuffer<ResourceAmount>(session);
            var info = em.GetBuffer<ResourceInfo>(session);
            using var specEntities = specQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (var e = 0; e < specEntities.Length; e++)
            {
                var specs = em.GetBuffer<ScenarioResourceSpec>(specEntities[e]);
                for (var i = 0; i < specs.Length; i++)
                {
                    var spec = specs[i];
                    if (spec.ResourceId.IsEmpty)
                        continue;
                    if (ResourceLedger.IndexOf(stock, spec.ResourceId) < 0)
                    {
                        Debug.LogError($"Scenario bake: unknown resource id {spec.ResourceId}.");
                        continue;
                    }

                    ResourceLedger.SetClamped(stock, info, spec.ResourceId, spec.Amount);
                }
            }
        }

        static void WritePendingPlaces(EntityManager em, Entity session)
        {
            if (!em.HasBuffer<PendingScenarioPlace>(session))
                em.AddBuffer<PendingScenarioPlace>(session);
            var pending = em.GetBuffer<PendingScenarioPlace>(session);
            pending.Clear();

            using var specQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ScenarioBuildingSpec>());
            if (specQuery.IsEmptyIgnoreFilter)
                return;

            if (!em.HasBuffer<BaseBuildingPrototype>(session)
                || em.GetBuffer<BaseBuildingPrototype>(session).Length == 0
                || !em.HasBuffer<BuildingPrototype>(session)
                || em.GetBuffer<BuildingPrototype>(session).Length == 0)
            {
                Debug.LogError("Scenario bake: session has no base/resolved building catalog.");
                return;
            }

            var toAdd = new NativeList<PendingScenarioPlace>(16, Allocator.Temp);
            var catalog = em.GetBuffer<BuildingPrototype>(session);
            using var specEntities = specQuery.ToEntityArray(Allocator.Temp);
            for (var e = 0; e < specEntities.Length; e++)
            {
                var specs = em.GetBuffer<ScenarioBuildingSpec>(specEntities[e]);
                for (var i = 0; i < specs.Length; i++)
                {
                    var spec = specs[i];
                    if (!BuildingCatalog.TryResolve(catalog, spec.TypeId, out var prototype))
                    {
                        Debug.LogError($"Scenario bake: unknown building type {spec.TypeId}.");
                        continue;
                    }

                    toAdd.Add(new PendingScenarioPlace
                    {
                        TypeId = prototype.TypeId,
                        Cluster = spec.Cluster,
                        Radial = spec.Radial
                    });
                }
            }

            pending = em.GetBuffer<PendingScenarioPlace>(session);
            for (var i = 0; i < toAdd.Length; i++)
                pending.Add(toAdd[i]);
            toAdd.Dispose();
        }

        static void WritePendingWorkers(EntityManager em, Entity session)
        {
            using var specQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ScenarioPopulation>());
            var workers = 0;
            if (!specQuery.IsEmptyIgnoreFilter)
            {
                using var specEntities = specQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                for (var e = 0; e < specEntities.Length; e++)
                    workers += math.max(0, em.GetComponentData<ScenarioPopulation>(specEntities[e]).StartingWorkers);
            }

            if (em.HasComponent<PendingScenarioSpawns>(session))
                em.SetComponentData(session, new PendingScenarioSpawns { Workers = workers });
            else
                em.AddComponentData(session, new PendingScenarioSpawns { Workers = workers });
        }
    }
}
