namespace TheyWillDescend.Shell
{
    public enum RunKind
    {
        Normal = 0,
        Debug = 1
    }

    /// <summary>
    /// One launch request. The menu passes it to <see cref="ShellService.EnterGame"/>;
    /// the service hands the same value to <see cref="GameSession.Begin"/>.
    /// </summary>
    public readonly struct RunLaunch
    {
        public readonly RunKind Kind;
        public readonly bool LoadSlot;

        public RunLaunch(RunKind kind, bool loadSlot)
        {
            Kind = kind;
            LoadSlot = loadSlot;
        }

        public static RunLaunch Normal => new RunLaunch(RunKind.Normal, false);

        public static RunLaunch Debug => new RunLaunch(RunKind.Debug, false);

        public static RunLaunch Slot => new RunLaunch(RunKind.Normal, true);
    }
}
