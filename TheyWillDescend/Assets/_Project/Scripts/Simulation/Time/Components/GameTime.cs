using Unity.Entities;

namespace TheyWillDescend.Simulation.Time
{
    public struct GameTime : IComponentData
    {
        public int Day;
        public float ElapsedInDay;
        public float DayDuration;
        public float WorkShiftStartHour;
        public float WorkShiftEndHour;

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

        /// <summary>Start inclusive, end exclusive. Night is plaza time.</summary>
        public bool IsWorkShift
        {
            get
            {
                var start = WorkShiftStartHour;
                var end = WorkShiftEndHour;
                if (end <= start)
                {
                    start = 6f;
                    end = 18f;
                }

                var hour = HourOfDay;
                return hour >= start && hour < end;
            }
        }
    }
}
