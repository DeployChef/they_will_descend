using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Ring around city center: unassigned always, assigned crew after 18:00.
    /// </summary>
    public struct AgentPlazaIdle : IComponentData
    {
        public float Timer;
        public float Angle;
        public float Radius;
        public byte Walking;
    }
}
