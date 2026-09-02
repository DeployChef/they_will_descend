using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Cached DefaultWorld + the singleton entity with global buffers.
    /// Not lookups. World is missing in Edit Mode and before SubScene bake.
    /// </summary>
    public static class SimWorld
    {
        static World _world;
        static Entity _bag;

        public static bool TryGet(out EntityManager em, out Entity bag)
        {
            em = default;
            bag = default;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            em = world.EntityManager;
            if (_world == world && em.Exists(_bag))
            {
                bag = _bag;
                return true;
            }

            if (!SimSessionAccess.TryGet(em, out bag))
            {
                _world = null;
                _bag = default;
                return false;
            }

            _world = world;
            _bag = bag;
            return true;
        }
    }
}
