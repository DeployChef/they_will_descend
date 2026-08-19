using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// ECS mirror of Shell clock policy. Written via <c>SimIo.SetClock</c> from the composition root.
    /// Systems consume <see cref="DeltaTime"/> — they do not interpret pause reasons.
    /// </summary>
    public struct SimControl : IComponentData
    {
        public SimRunMode Mode;
        public int Speed;
        public float DeltaTime;
    }
}
