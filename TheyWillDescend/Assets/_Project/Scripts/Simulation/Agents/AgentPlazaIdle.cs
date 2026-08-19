using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Unassigned idle around city center: stand or walk the ring.
    /// </summary>
    public struct AgentPlazaIdle : IComponentData
    {
        public float Timer;
        public float Angle;
        public float Radius;
        public byte Walking;
    }
}
