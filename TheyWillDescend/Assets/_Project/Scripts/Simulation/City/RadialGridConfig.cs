using System;

namespace _Project.Scripts.Simulation.City
{
    /// <summary>
    /// Polar city grid.
    /// Rings are shared. Fine vs quantum differs only in angular section density:
    /// quantum = AngularQuantum consecutive fine wedges.
    /// </summary>
    [Serializable]
    public struct RadialGridConfig
    {
        /// <summary>Empty plaza / forbidden zone around CityCenter.</summary>
        public float InnerRadius;

        /// <summary>World distance from one ring to the next (shared by fine and quantum).</summary>
        public float RadialStep;

        /// <summary>Number of concentric rings (shared). e.g. 10.</summary>
        public int RingCount;

        /// <summary>Fine angular wedges in a full circle. Must be divisible by AngularQuantum.</summary>
        public int AngularDivisions;

        /// <summary>Fine wedges per building quantum (angular only).</summary>
        public int AngularQuantum;

        public static RadialGridConfig Default => new()
        {
            InnerRadius = 5f,
            RadialStep = 3f,
            RingCount = 10,
            AngularDivisions = 240,
            AngularQuantum = 5
        };

        /// <summary>Inclusive max fine radial index (= RingCount - 1).</summary>
        public int MaxRadialIndex => RingCount - 1;

        public int AngularQuantaCount =>
            AngularQuantum > 0 ? AngularDivisions / AngularQuantum : 0;

        public bool IsValid =>
            InnerRadius >= 0f
            && RadialStep > 0f
            && RingCount > 0
            && AngularDivisions > 0
            && AngularQuantum > 0
            && AngularDivisions % AngularQuantum == 0;

        /// <summary>
        /// World radius of ring line i (1..RingCount). RingCount is the outer edge.
        /// Same radii for fine gizmos and quantum underlay.
        /// </summary>
        public float RingLineRadius(int ringNumber)
        {
            return InnerRadius + ringNumber * RadialStep;
        }
    }
}
