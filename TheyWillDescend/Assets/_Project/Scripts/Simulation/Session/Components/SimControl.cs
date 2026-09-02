using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Session clock. UI writes via <c>SimClockCommand</c>; systems read Mode / Speed / DeltaTime.
    /// <see cref="DeltaTime"/> is frame length times Speed — not zeroed on pause.
    /// </summary>
    public struct SimControl : IComponentData
    {
        public SimRunMode Mode;
        public int Speed;
        public float DeltaTime;
        public byte SessionInGame;
        public byte PlayerPaused;
        public byte BuildLocked;
        /// <summary>
        /// 0 until the session publisher or a loaded snapshot applied.
        /// Scenario pending and Place commands must not spawn before that.
        /// </summary>
        public byte RunPrepared;

        public bool IsRunning => Mode == SimRunMode.Running;
    }
}
