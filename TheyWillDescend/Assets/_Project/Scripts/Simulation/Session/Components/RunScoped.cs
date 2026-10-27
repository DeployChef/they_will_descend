using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Everything that dies with the run. Prototypes carry the tag so Instantiate copies it.
    /// </summary>
    public struct RunScoped : IComponentData
    {
    }
}
