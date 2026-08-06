using Unity.Entities;

namespace _Project.Scripts.Simulation.Time
{
    public struct GameTime : IComponentData
    {
        public int Day;
        public float ElapsedInDay;
        public float DayDuration;
    }
}
