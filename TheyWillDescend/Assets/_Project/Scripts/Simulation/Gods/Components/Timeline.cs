using Unity.Entities;

namespace TheyWillDescend.Simulation.Gods
{
    /// <summary>
    /// Current era index and the loyalty-cap lerp window. Tribute set is EraIndex.
    /// </summary>
    public struct Timeline : IComponentData
    {
        public int EraIndex;
        public int EraStartDay;
        public float EraStartElapsed;
        public float PreviousMaxLoyalty;
        public float TargetMaxLoyalty;
    }
}
