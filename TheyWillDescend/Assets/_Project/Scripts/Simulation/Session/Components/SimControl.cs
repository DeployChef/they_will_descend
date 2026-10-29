using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Session clock. A new run starts <see cref="SimRunMode.Running"/> at speed 1.
    /// A save restores speed and pause. Zeroed Mode is <see cref="SimRunMode.Off"/> and does not tick.
    /// <see cref="DeltaTime"/> is frame length times Speed — not zeroed on pause.
    /// <see cref="TimePaused"/> is the HUD ⏸ (clock stands, city stays playable).
    /// <see cref="PlayerPaused"/> is Esc overlay only.
    /// </summary>
    public struct SimControl : IComponentData
    {
        public SimRunMode Mode;
        public int Speed;
        public float DeltaTime;
        public byte TimePaused;
        public byte PlayerPaused;
        public byte BuildLocked;

        public bool IsRunning => Mode == SimRunMode.Running;
    }
}
