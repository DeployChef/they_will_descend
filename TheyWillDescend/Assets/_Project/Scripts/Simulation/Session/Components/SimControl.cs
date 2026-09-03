using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Session clock. UI writes via <c>SimClockCommand</c>; systems read Mode / Speed / DeltaTime.
    /// <see cref="DeltaTime"/> is frame length times Speed — not zeroed on pause.
    /// <see cref="TimePaused"/> is the HUD ⏸ (clock stands, city stays playable).
    /// <see cref="PlayerPaused"/> is Esc overlay only.
    /// </summary>
    public struct SimControl : IComponentData
    {
        public SimRunMode Mode;
        public int Speed;
        public float DeltaTime;
        public byte SessionInGame;
        public byte TimePaused;
        public byte PlayerPaused;
        public byte BuildLocked;

        public bool IsRunning => Mode == SimRunMode.Running;
    }
}
