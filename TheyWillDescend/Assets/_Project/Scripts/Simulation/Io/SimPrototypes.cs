using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// Baked entity stamps for Instantiate. Not GameObject prefabs.
    /// Agent is a blank sim body. Houses live in <see cref="TheyWillDescend.Simulation.City.BuildingPrototype"/> on the same session.
    /// </summary>
    public struct SimPrototypes : IComponentData
    {
        public Entity Agent;
    }
}
