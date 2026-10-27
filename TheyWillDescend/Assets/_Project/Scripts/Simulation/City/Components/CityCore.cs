using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Session handle to unique city entities. Written by bootstrap.
    /// </summary>
    public struct CityCore : IComponentData
    {
        public Entity Headquarters;
    }
}
