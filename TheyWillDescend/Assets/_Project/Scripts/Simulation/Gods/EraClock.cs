using TheyWillDescend.Simulation.Time;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Gods
{
    public static class EraClock
    {
        public static float HourToElapsed(float hour, float dayDuration)
        {
            var duration = dayDuration > 0.0001f ? dayDuration : 1f;
            var h = hour;
            if (h < 0f)
                h = 0f;
            else if (h >= 24f)
                h = 23.999f;
            return h / 24f * duration;
        }

        public static bool Reached(in GameTime time, int day, float elapsed)
        {
            if (time.Day > day)
                return true;
            if (time.Day < day)
                return false;
            return time.ElapsedInDay + 0.0001f >= elapsed;
        }

        public static float HoursSince(
            in GameTime time,
            int startDay,
            float startElapsed)
        {
            var duration = time.DayDuration > 0.0001f ? time.DayDuration : 1f;
            var startHours = startDay * 24f + startElapsed / duration * 24f;
            var nowHours = time.Day * 24f + time.HourOfDay;
            return nowHours - startHours;
        }

        public static void StartOfEra(
            DynamicBuffer<EraLine> eras,
            float eraChangeHour,
            float dayDuration,
            int eraIndex,
            out int day,
            out float elapsed)
        {
            day = 0;
            elapsed = 0f;
            if (eraIndex <= 0)
                return;

            var startDay = 0;
            var last = eraIndex;
            if (last > eras.Length)
                last = eras.Length;
            for (var i = 0; i < last; i++)
            {
                var days = eras[i].DurationDays;
                startDay += days > 0 ? days : 1;
            }

            day = startDay;
            elapsed = HourToElapsed(eraChangeHour, dayDuration);
        }
    }
}
