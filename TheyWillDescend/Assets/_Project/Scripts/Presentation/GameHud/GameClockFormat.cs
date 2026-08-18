using _Project.Scripts.Simulation.Time;
using UnityEngine;

namespace _Project.Scripts.Presentation.GameHud
{
    public static class GameClockFormat
    {
        public static string Format(in GameTime time)
        {
            var duration = Mathf.Max(0.0001f, time.DayDuration);
            var dayT = Mathf.Clamp01(time.ElapsedInDay / duration);
            var totalMinutes = Mathf.FloorToInt(dayT * 24f * 60f);
            if (totalMinutes >= 24 * 60)
                totalMinutes = 24 * 60 - 1;

            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            return $"Day {time.Day}  {hours:00}:{minutes:00}";
        }
    }
}
