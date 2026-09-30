using System;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// App-lifetime facts that must survive scene loads. Not the simulation session.
    /// One instance on the root scope.
    /// </summary>
    public sealed class AppContext
    {
        public bool IsFirstStart { get; set; }

        public RunLaunch Launch { get; private set; }

        public bool EnteringGame { get; private set; }

        public event Action LaunchRequested;

        public void RequestLaunch(RunLaunch launch)
        {
            Launch = launch;
            IsFirstStart = true;
            EnteringGame = true;
            LaunchRequested?.Invoke();
        }

        public void MarkLaunchStarted() => EnteringGame = false;
    }
}
