using Unity.Entities;

namespace TheyWillDescend.Simulation.Gods
{
    /// <summary>
    /// World numbers for the HQ furnace and timeline.
    /// </summary>
    public struct PyramidConfig : IComponentData
    {
        public float EraChangeHour;
        public float DefaultStockCap;
        public float LoyaltyDecayPerDay;
    }
}
