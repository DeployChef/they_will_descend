using System;
using TheyWillDescend.Presentation.SimBridge;

namespace TheyWillDescend.Presentation.Shell
{
    /// <summary>
    /// One launch request. The menu passes it to <see cref="ShellService.StartRun"/>,
    /// which stores it on <see cref="AppContext.CurrentLaunch"/>.
    /// <see cref="GameEntry"/> reads that and hands it to <see cref="SimulationHost.Open"/>.
    /// A save launch always names the snapshot. There is no "load the slot" without an id.
    /// </summary>
    public readonly struct RunLaunch
    {
        public readonly bool Debug;
        public readonly string SaveId;

        RunLaunch(bool debug, string saveId)
        {
            Debug = debug;
            SaveId = saveId;
        }

        public bool IsSave => !string.IsNullOrEmpty(SaveId);

        public static RunLaunch New(bool debug = false) => new RunLaunch(debug, null);

        public static RunLaunch FromSave(string saveId)
        {
            if (string.IsNullOrWhiteSpace(saveId))
                throw new ArgumentException("A save launch needs a save id.", nameof(saveId));
            return new RunLaunch(false, saveId.Trim());
        }

        public override string ToString() => IsSave ? SaveId : Debug ? "Debug" : "New";
    }
}
