using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    public static class BuildingDismantle
    {
        public static void Complete(EntityManager em, Entity site)
        {
            if (!em.Exists(site) || !em.HasComponent<Building>(site))
                return;

            var building = em.GetComponentData<Building>(site);
            ReleaseCrew(em, building.Id);
            if (SimSessionAccess.TryGet(em, out var session))
            {
                Refund(em, session, building.TypeId);
                FreeCells(em, session, building);
            }

            em.DestroyEntity(site);
        }

        public static void ReleaseCrew(EntityManager em, int buildingId)
        {
            if (buildingId <= 0)
                return;

            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<AgentAssignment>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            var assignments = query.ToComponentDataArray<AgentAssignment>(Allocator.Temp);
            for (var i = 0; i < assignments.Length; i++)
            {
                var job = assignments[i];
                if (job.ConstructionBuildingId != buildingId)
                    continue;
                job.ConstructionBuildingId = 0;
                job.Arrived = 0;
                em.SetComponentData(entities[i], job);
            }

            assignments.Dispose();
        }

        static void Refund(EntityManager em, Entity session, in FixedString64Bytes typeId)
        {
            if (!em.HasBuffer<BuildingCatalogCost>(session) || !em.HasBuffer<ResourceAmount>(session))
                return;
            var info = em.HasBuffer<ResourceInfo>(session)
                ? em.GetBuffer<ResourceInfo>(session)
                : default;
            if (!info.IsCreated)
                return;
            BuildingCosts.Refund(
                em.GetBuffer<BuildingCatalogCost>(session),
                typeId,
                em.GetBuffer<ResourceAmount>(session),
                info);
        }

        static void FreeCells(EntityManager em, Entity session, in Building building)
        {
            if (!em.HasComponent<CityGrid>(session) || !em.HasBuffer<OccupiedCell>(session))
                return;

            var grid = em.GetComponentData<CityGrid>(session);
            var footprint = new BuildingFootprint
            {
                WidthClusters = building.WidthClusters,
                DepthRadialRings = building.DepthRadialRings
            };
            var clusters = new NativeList<OccupiedCell>(64, Allocator.Temp);
            if (!RadialFootprintMath.TryExpandClusters(
                    grid.Config, building.AnchorCluster, building.AnchorRadial, footprint, clusters))
            {
                clusters.Dispose();
                return;
            }

            var occupied = em.GetBuffer<OccupiedCell>(session);
            for (var i = 0; i < clusters.Length; i++)
            {
                var cell = clusters[i];
                for (var j = occupied.Length - 1; j >= 0; j--)
                {
                    if (occupied[j].Cluster != cell.Cluster || occupied[j].Radial != cell.Radial)
                        continue;
                    occupied.RemoveAt(j);
                    break;
                }
            }

            clusters.Dispose();
        }
    }
}
