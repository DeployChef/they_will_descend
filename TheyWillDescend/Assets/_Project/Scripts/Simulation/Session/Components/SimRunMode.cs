namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Session run mode on <see cref="SimControl"/>.
    /// Simulation systems run gameplay only when <see cref="Running"/>.
    /// <see cref="Off"/> is the zero value. The host writes <see cref="Running"/> or <see cref="Frozen"/> before systems see the session.
    /// <see cref="Frozen"/> is a live session with pause or the build lock.
    /// </summary>
    public enum SimRunMode : byte
    {
        Off = 0,
        Running = 1,
        Frozen = 2
    }
}
