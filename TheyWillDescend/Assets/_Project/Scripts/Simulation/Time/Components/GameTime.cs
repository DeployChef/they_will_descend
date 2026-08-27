using Unity.Entities;

namespace TheyWillDescend.Simulation.Time
{
    public struct GameTime : IComponentData
    {
        public const float WorkShiftStartHour = 6f;
        public const float WorkShiftEndHour = 18f;

        public int Day;
        public float ElapsedInDay;
        public float DayDuration;

        public float HourOfDay
        {
            get
            {
                var duration = DayDuration > 0.0001f ? DayDuration : 1f;
                var t = ElapsedInDay / duration;
                if (t < 0f)
                    t = 0f;
                else if (t >= 1f)
                    t = 0.99999f;
                return t * 24f;
            }
        }

        /// <summary>06:00 inclusive … 18:00 exclusive. Night is plaza time.</summary>
        public bool IsWorkShift
        {
            get
            {
                var hour = HourOfDay;
                return hour >= WorkShiftStartHour && hour < WorkShiftEndHour;
            }
        }
    }
}
