namespace TheyWillDescend.Simulation.Session
{
    /// <summary>
    /// Session run mode on <see cref="SimControl"/>.
    /// Simulation systems may run gameplay logic only when <see cref="Running"/>.
    /// </summary>
    public enum SimRunMode : byte
    {
        Off = 0,
        Running = 1,
        Frozen = 2
    }
}
