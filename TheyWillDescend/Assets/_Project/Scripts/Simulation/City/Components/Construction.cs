using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Present = house is not in the finished state. Same stamp entity.
    /// Raising: Elapsed grows to Duration. Dismantling: Elapsed falls to 0.
    /// Crew on site required to move Elapsed.
    /// </summary>
    public struct Construction : IComponentData
    {
        public float Elapsed;
        public float Duration;
        public byte Dismantling;

        public readonly bool IsDismantling => Dismantling != 0;

        public readonly float Normalized =>
            Duration <= 0.0001f
                ? (IsDismantling ? 0f : 1f)
                : math.clamp(Elapsed / Duration, 0f, 1f);

        public readonly bool IsComplete => IsDismantling
            ? Elapsed <= 0.0001f
            : Duration <= 0.0001f || Elapsed >= Duration;
    }
}
