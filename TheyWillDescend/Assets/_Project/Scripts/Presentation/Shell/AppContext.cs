namespace TheyWillDescend.Presentation.Shell
{
    /// <summary>
    /// Facts of this process. A new player start creates a new instance; nothing here is static.
    /// <see cref="CurrentLaunch"/> stays until the next start or restart overwrites it.
    /// </summary>
    public sealed class AppContext
    {
        public RunLaunch? CurrentLaunch { get; set; }

        public bool SplashShown { get; set; }
    }
}
