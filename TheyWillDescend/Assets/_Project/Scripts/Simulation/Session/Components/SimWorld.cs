using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Research;
using TheyWillDescend.Simulation.Stories;
using TheyWillDescend.Simulation.Time;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Cached DefaultWorld + the session entity. Queries live with the World, not with HUD.
    /// </summary>
    public static class SimWorld
    {
        static World _world;
        static Entity _bag;
        static EntityQuery _liveDialogs;
        static EntityQuery _liveQuests;
        static EntityQuery _techInfo;
        static EntityQuery _finishedHouses;

        public static bool TryGet(out EntityManager em, out Entity bag)
        {
            em = default;
            bag = default;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            em = world.EntityManager;
            if (_world != world)
                Bind(world);

            if (_world == world && em.Exists(_bag))
            {
                bag = _bag;
                return true;
            }

            if (!SimSessionAccess.TryGet(em, out bag))
            {
                Bind(null);
                return false;
            }

            _world = world;
            _bag = bag;
            return true;
        }

        public static bool TryGetEntityManager(out EntityManager em)
        {
            em = default;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;
            em = world.EntityManager;
            return true;
        }

        public static bool TryGetGameTime(EntityManager em, Entity bag, out GameTime time)
        {
            if (!em.HasComponent<GameTime>(bag))
            {
                time = default;
                return false;
            }

            time = em.GetComponentData<GameTime>(bag);
            return true;
        }

        public static bool TrySetGameTime(in GameTime time)
        {
            if (!TryGet(out var em, out var bag) || !em.HasComponent<GameTime>(bag))
                return false;
            em.SetComponentData(bag, time);
            return true;
        }

        public static EntityQuery LiveDialogs(EntityManager em) =>
            Cached(ref _liveDialogs, em, ComponentType.ReadOnly<StoryLiveDialog>());

        public static EntityQuery LiveQuests(EntityManager em) =>
            Cached(ref _liveQuests, em, ComponentType.ReadOnly<StoryLiveQuest>());

        public static EntityQuery TechCards(EntityManager em) =>
            Cached(ref _techInfo, em, ComponentType.ReadOnly<TechInfo>());

        public static EntityQuery FinishedHouses(EntityManager em) =>
            Cached(
                ref _finishedHouses,
                em,
                ComponentType.ReadOnly<Building>(),
                ComponentType.Exclude<Construction>());

        static EntityQuery Cached(ref EntityQuery query, EntityManager em, params ComponentType[] types)
        {
            var world = em.World;
            if (_world != world)
                Bind(world);
            if (query == default)
                query = em.CreateEntityQuery(types);
            return query;
        }

        static void Bind(World world)
        {
            _world = world;
            _bag = default;
            _liveDialogs = default;
            _liveQuests = default;
            _techInfo = default;
            _finishedHouses = default;
        }
    }
}
