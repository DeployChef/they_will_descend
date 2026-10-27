using TheyWillDescend.Content;
using TheyWillDescend.Presentation.Infrastructure;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Research;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Presentation.SimBridge
{
    /// <summary>
    /// Seeds a fresh run from a scenario. Houses and workers exist before the first tick.
    /// </summary>
    public static class ScenarioSetup
    {
        public static bool Apply(
            EntityManager em,
            Entity session,
            ScenarioDefinition scenario,
            TechCatalogAsset[] techCatalogs,
            float workerSpeed,
            BuildingPrototype[] catalog,
            BuildingCatalogRecipe[] recipes,
            BuildingCatalogCost[] costs,
            ResourceAmount[] stock,
            ResourceInfo[] info)
        {
            if (!em.Exists(session) || !em.HasComponent<SimSession>(session))
            {
                GameLog.Error("ScenarioSetup: SimSession missing.");
                return false;
            }

            WriteStock(stock, info, scenario);
            var houses = PlaceHouses(em, session, scenario, catalog, recipes);
            SimulationBootstrap.SpawnRunEntities(em, session, workerSpeed, info);
            var techs = ResearchWorld.Populate(em, techCatalogs);
            SeedInnerRing(em, session);
            var workers = scenario != null ? scenario.StartingWorkers : 0;
            SpawnWorkers(em, session, workers);

            try
            {
                SimulationBootstrap.WriteCatalog(em, session, catalog, recipes, costs, stock, info);
            }
            catch (System.Exception e)
            {
                GameLog.Error($"ScenarioSetup: catalog buffers ({e.Message}).");
            }
            var name = scenario != null ? scenario.name : "none";
            GameLog.Info(
                $"Run seeded: scenario={name}, houses={houses}, workers={workers}, techs={techs}.");
            return true;
        }

        static int PlaceHouses(
            EntityManager em,
            Entity session,
            ScenarioDefinition scenario,
            BuildingPrototype[] catalog,
            BuildingCatalogRecipe[] recipes)
        {
            if (scenario == null || !em.HasComponent<CityGrid>(session))
                return 0;

            var grid = em.GetComponentData<CityGrid>(session);
            var placed = 0;
            var buildings = scenario.Buildings;
            GameLog.Info(
                $"PlaceHouses records={buildings.Count} catalog={(catalog != null ? catalog.Length : 0)}.");
            for (var i = 0; i < buildings.Count; i++)
            {
                var record = buildings[i];
                var typeId = ContentId.EncodeOrEmpty(record.TypeId);
                if (typeId.IsEmpty)
                    continue;

                var spec = new BuildingPlaceSpec
                {
                    TypeId = typeId,
                    AnchorCluster = record.Cluster,
                    AnchorRadial = record.Radial,
                    InstantComplete = 1,
                    Mode = BuildingPlaceMode.Setup
                };
                if (BuildingPlacement.TryPlace(em, session, ref grid, in spec, catalog, recipes))
                    placed++;
            }

            em.SetComponentData(session, grid);
            return placed;
        }

        static void SeedInnerRing(EntityManager em, Entity session)
        {
            if (!em.HasComponent<CityGrid>(session) || !em.HasComponent<RoadNetwork>(session))
                return;
            var grid = em.GetComponentData<CityGrid>(session);
            var network = em.GetComponentData<RoadNetwork>(session);
            RoadSections.SeedInnerRing(em, session, ref grid, ref network);
            em.SetComponentData(session, grid);
            em.SetComponentData(session, network);
        }

        static void WriteStock(ResourceAmount[] stock, ResourceInfo[] info, ScenarioDefinition scenario)
        {
            if (stock == null)
                return;
            for (var i = 0; i < stock.Length; i++)
            {
                var row = stock[i];
                row.Amount = 0f;
                stock[i] = row;
            }

            if (scenario == null || scenario.StartingStock == null || info == null)
                return;
            var rows = scenario.StartingStock;
            for (var i = 0; i < rows.Count; i++)
            {
                var start = rows[i];
                if (start.Resource == null)
                    continue;
                var id = ContentId.EncodeOrEmpty(start.Resource.ResourceId);
                if (id.IsEmpty)
                    continue;
                var cap = 0f;
                for (var n = 0; n < info.Length; n++)
                {
                    if (info[n].ResourceId != id)
                        continue;
                    cap = info[n].StockCap;
                    break;
                }

                for (var s = 0; s < stock.Length; s++)
                {
                    if (stock[s].ResourceId != id)
                        continue;
                    var row = stock[s];
                    row.Amount = cap > 0.0001f ? math.clamp(start.Amount, 0f, cap) : math.max(0f, start.Amount);
                    stock[s] = row;
                    break;
                }
            }
        }

        static void SpawnWorkers(EntityManager em, Entity session, int count)
        {
            if (count <= 0 || !em.HasComponent<CityGrid>(session))
                return;

            var grid = em.GetComponentData<CityGrid>(session);
            var center = grid.Center;
            for (var i = 0; i < count; i++)
            {
                var turns = count == 1 ? 0f : i / (float)count;
                var angle = turns * 2f * math.PI;
                var radius = PlazaRing.Lane(grid.Config, i);
                var position = new float3(
                    center.x + math.cos(angle) * radius,
                    center.y,
                    center.z + math.sin(angle) * radius);
                var direction = PlazaRing.PickDirection(i * 17 + 3);
                var walking = (byte)(i % 4 == 0 ? 0 : 1);
                var tangent = new float3(-math.sin(angle), 0f, math.cos(angle)) * direction;
                var inward = new float3(-math.cos(angle), 0f, -math.sin(angle));

                AgentSpawn.Spawn(em, session, new SpawnAgentRequest
                {
                    Position = position,
                    Facing = walking != 0 ? tangent : inward,
                    Speed = 0f,
                    HasPose = 1,
                    PlazaWalking = walking,
                    PlazaDirection = direction,
                    PlazaAngle = angle,
                    PlazaRadius = radius,
                    PlazaTimer = walking != 0 ? 3f + (i % 5) * 0.9f : 1.5f + (i % 3) * 0.5f,
                    Kind = AgentKind.Worker
                });
            }
        }
    }
}
