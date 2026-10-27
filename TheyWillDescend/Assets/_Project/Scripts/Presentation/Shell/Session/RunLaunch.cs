namespace TheyWillDescend.Presentation.Shell
{
    public enum RunKind
    {
        Normal = 0,
        Debug = 1
    }

    /// <summary>
    /// One launch request. The menu passes it to <see cref="ShellService.StartRun"/>;
    /// <see cref="GameEntry"/> takes it back and hands it to <see cref="GameSession.Begin"/>.
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

        public override string ToString() => LoadSlot ? "Slot" : Kind.ToString();
    }

    /// <summary>
    /// Run that Game starts when it is opened directly in the editor. Builds always start Normal.
    /// </summary>
    public enum EditorRunLaunch
    {
        Normal = 0,
        Debug = 1,
        Slot = 2
    }

    public static class EditorRunLaunchExtensions
    {
        public static RunLaunch ToLaunch(this EditorRunLaunch launch) => launch switch
        {
            EditorRunLaunch.Debug => RunLaunch.Debug,
            EditorRunLaunch.Slot => RunLaunch.Slot,
            _ => RunLaunch.Normal
        };
    }
}
