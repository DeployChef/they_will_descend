using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Staffing of one house. Slots live on <see cref="BuildingType"/>.
    /// Recipe rates at 10/10 = 100%; production scales with <see cref="WorkingCount"/>.
    /// </summary>
    public struct Workplace : IComponentData
    {
        public int AssignedCount;
        public int WorkingCount;

        public static float Load01(int count, int slots)
        {
            if (slots <= 0 || count <= 0)
                return 0f;
            var load = (float)count / slots;
            return load > 1f ? 1f : load;
        }
    }
}
