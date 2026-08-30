using Unity.Entities;

namespace TheyWillDescend.Simulation.Gods
{
    /// <summary>
    /// World numbers for the HQ furnace. Obelisks later add to MaxEnergyPerHour.
    /// </summary>
    public struct PyramidConfig : IComponentData
    {
        public float MaxEnergyPerHour;
        public float EraChangeHour;
        public float DefaultStockCap;
        public float LoyaltyDecayPerDay;
    }
}
