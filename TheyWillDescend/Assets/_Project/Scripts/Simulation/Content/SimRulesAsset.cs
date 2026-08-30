using TheyWillDescend.Simulation.Time;
using UnityEngine;

namespace TheyWillDescend.Simulation.Content
{
    /// <summary>
    /// World-tick balance. Not a building type and not a starting city.
    /// Baker copies numbers onto <see cref="GameTime"/> and the agent stamp.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SimRules",
        menuName = "They Will Descend/Sim Rules")]
    public sealed class SimRulesAsset : ScriptableObject
    {
        [SerializeField] [Min(1f)] float dayDuration = 60f;
        [SerializeField] [Range(0f, 23f)] float workShiftStartHour = 6f;
        [SerializeField] [Range(1f, 24f)] float workShiftEndHour = 18f;
        [SerializeField] [Min(0.01f)] float workerSpeed = 2f;
        [SerializeField] [Range(0f, 23f)] float eraChangeHour = 8f;
        [SerializeField] [Min(0.01f)] float pyramidMaxEnergyPerHour = 24f;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Loyalty points lost per game day if you do nothing. Tribute offsets this.")]
        float loyaltyDecayPerDay = 12f;
        [SerializeField]
        [Min(1f)]
        [Tooltip("Temporary stock ceiling until warehouses exist.")]
        float defaultStockCap = 2000f;

        public float DayDuration => dayDuration > 0.001f ? dayDuration : 60f;
        public float WorkShiftStartHour => workShiftStartHour;
        public float WorkShiftEndHour => workShiftEndHour;
        public float WorkerSpeed => workerSpeed > 0.001f ? workerSpeed : 2f;
        public float EraChangeHour => eraChangeHour;
        public float PyramidMaxEnergyPerHour =>
            pyramidMaxEnergyPerHour > 0.001f ? pyramidMaxEnergyPerHour : 24f;
        public float LoyaltyDecayPerDay => loyaltyDecayPerDay < 0f ? 0f : loyaltyDecayPerDay;
        public float DefaultStockCap => defaultStockCap > 0.001f ? defaultStockCap : 2000f;

        public GameTime CreateClock()
        {
            return new GameTime
            {
                Day = 0,
                ElapsedInDay = 0f,
                DayDuration = DayDuration,
                WorkShiftStartHour = WorkShiftStartHour,
                WorkShiftEndHour = WorkShiftEndHour
            };
        }
    }
}
