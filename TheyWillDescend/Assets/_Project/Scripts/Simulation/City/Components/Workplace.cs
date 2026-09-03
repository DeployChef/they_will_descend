using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Staffing of one house. Slots live on <see cref="BuildingType"/>.
    /// Recipe rates at 10/10 = 100%; production scales with <see cref="WorkingCount"/>.
    /// </summary>
    public struct Workplace : IComponentData
    {
        public int DesiredWorkers;
        public int AssignedCount;
        public int WorkingCount;
        /// <summary>0 = operating. 1 = recipe halted; assigned workers stay.</summary>
        public byte Paused;


        public bool IsPaused => Paused != 0;

        public static float Load01(int count, int slots)
        {
            if (slots <= 0 || count <= 0)
                return 0f;
            var load = (float)count / slots;
            return load > 1f ? 1f : load;
        }
    }
}
