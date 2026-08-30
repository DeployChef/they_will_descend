using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Gods;
using Unity.Entities;

namespace TheyWillDescend.Authoring.City
{
    /// <summary>
    /// Fills HQ feed sliders from catalog resources that can feed the pyramid.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    [UpdateAfter(typeof(HeadquartersCenterBakeSystem))]
    public partial struct PyramidFeedBakeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<ResourceInfo>(out var session))
                return;
            if (!SystemAPI.HasBuffer<ResourceInfo>(session))
                return;

            var em = state.EntityManager;
            var info = em.GetBuffer<ResourceInfo>(session);
            foreach (var (feed, _) in
                     SystemAPI.Query<DynamicBuffer<PyramidFeedLine>, RefRO<Headquarters>>())
            {
                feed.Clear();
                for (var i = 0; i < info.Length; i++)
                {
                    var row = info[i];
                    if (row.CanFeed == 0 || row.ResourceId.IsEmpty)
                        continue;
                    feed.Add(new PyramidFeedLine
                    {
                        ResourceId = row.ResourceId,
                        PerHour = 0f
                    });
                }
            }
        }
    }
}
