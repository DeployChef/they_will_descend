using Unity.Entities;

namespace _Project.Scripts.Simulation.Session
{
    /// <summary>
    /// Singleton: whether the city simulation is allowed to tick.
    /// Written by Shell (<c>SimGate</c>), read by ECS systems.
    /// </summary>
    public struct SimControl : IComponentData
    {
        public SimRunMode Mode;
    }
}
