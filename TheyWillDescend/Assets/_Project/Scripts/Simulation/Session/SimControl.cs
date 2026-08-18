using Unity.Entities;

namespace _Project.Scripts.Simulation.Session
{
    /// <summary>
    /// ECS mirror of Shell clock policy. Written by <c>SimControlSyncSystem</c>.
    /// Systems consume <see cref="DeltaTime"/> — they do not interpret pause reasons.
    /// </summary>
    public struct SimControl : IComponentData
    {
        public SimRunMode Mode;
        public int Speed;
        public float DeltaTime;
    }
}
