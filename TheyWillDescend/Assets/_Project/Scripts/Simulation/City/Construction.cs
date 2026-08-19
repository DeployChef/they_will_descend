using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Present = still building. Mesh stamp is instantiated only after this is removed.
    /// </summary>
    public struct Construction : IComponentData
    {
        public float Elapsed;
        public float Duration;

        public readonly float Normalized =>
            Duration <= 0.0001f ? 1f : math.min(1f, Elapsed / Duration);

        public readonly bool IsComplete => Duration > 0f && Elapsed >= Duration;
    }
}
