using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumeSpawnAgentCommandsSystem))]
    public partial struct ConsumePlaceBuildingCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
            state.RequireForUpdate<CityGrid>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SimSessionAccess.TryGet(em, out var session))
                return;

            var query = SystemAPI.QueryBuilder().WithAll<PlaceBuildingRequest>().Build();
            if (query.IsEmptyIgnoreFilter)
                return;

            var inGame = em.GetComponentData<SimControl>(session).SessionInGame != 0;
            var grid = em.GetComponentData<CityGrid>(session);
            using var requestEntities = query.ToEntityArray(Allocator.Temp);
            using var requests = query.ToComponentDataArray<PlaceBuildingRequest>(Allocator.Temp);
            BuildingPlacement.CopyCatalog(em, session, out var catalog, out var recipes);

            for (var i = 0; i < requests.Length; i++)
            {
                if (inGame)
                {
                    var spec = new BuildingPlaceSpec
                    {
                        TypeId = requests[i].TypeId,
                        AnchorCluster = requests[i].AnchorCluster,
                        AnchorRadial = requests[i].AnchorRadial,
                        Mode = BuildingPlaceMode.Gameplay
                    };
                    BuildingPlacement.TryPlace(em, session, ref grid, in spec, catalog, recipes);
                }

                em.DestroyEntity(requestEntities[i]);
            }

            em.SetComponentData(session, grid);
        }
    }
}
