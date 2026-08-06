using System;

namespace _Project.Scripts.Simulation.City
{
    /// <summary>Discrete fine cell on the polar city grid.</summary>
    [Serializable]
    public struct RadialCell : IEquatable<RadialCell>
    {
        public int AngularIndex;
        public int RadialIndex;

        public RadialCell(int angularIndex, int radialIndex)
        {
            AngularIndex = angularIndex;
            RadialIndex = radialIndex;
        }

        public bool Equals(RadialCell other) =>
            AngularIndex == other.AngularIndex && RadialIndex == other.RadialIndex;

        public override bool Equals(object obj) => obj is RadialCell other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(AngularIndex, RadialIndex);

        public override string ToString() => $"({AngularIndex},{RadialIndex})";
    }
}
