using Unity.Entities;

namespace TheyWillDescend.Simulation.Time
{
    public struct GameTime : IComponentData
    {
        public int Day;
        public float ElapsedInDay;
        public float DayDuration;
    }
}
