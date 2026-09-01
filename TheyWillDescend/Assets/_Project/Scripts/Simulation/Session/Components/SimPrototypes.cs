using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Agent is a blank sim body for Instantiate. Houses are not entity prefabs —
    /// their spec lives in <see cref="TheyWillDescend.Simulation.City.BuildingPrototype"/> on the same session.
    /// </summary>
    public struct SimPrototypes : IComponentData
    {
        public Entity Agent;
    }
}
