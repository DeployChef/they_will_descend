using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    public enum SimSessionPhase : byte
    {
        Unprepared = 0,
        Preparing = 1,
        Ready = 2,
        Resetting = 3
    }

    /// <summary>
    /// Unmanaged lifecycle state for the ECS session root.
    /// </summary>
    public struct SimSession : IComponentData
    {
        public SimSessionPhase Phase;

        public readonly bool AcceptsSetupCommands => Phase == SimSessionPhase.Preparing;
        public readonly bool IsReady => Phase == SimSessionPhase.Ready;
    }
}
