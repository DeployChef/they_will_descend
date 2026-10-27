using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Marker on the session singleton. Catalogs, clock, and ledgers live here.
    /// </summary>
    public struct SimSession : IComponentData
    {
    }
}
