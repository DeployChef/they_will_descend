using TheyWillDescend.Simulation.City;
using Unity.Entities;
using Unity.Transforms;

namespace TheyWillDescend.Authoring.City
{
    /// <summary>
    /// HQ transform is the polar origin. Writes CityGrid.Center at bake.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    public partial struct HeadquartersCenterBakeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonRW<CityGrid>(out var grid))
                return;

            foreach (var transform in
                     SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Headquarters>())
            {
                grid.ValueRW.Center = transform.ValueRO.Position;
                return;
            }
        }
    }
}
