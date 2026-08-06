using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Simulation.Session;

namespace _Project.Scripts.Shell
{
    /// <summary>
    /// Desired simulation run mode owned by Shell.
    /// Does not touch EntityManager — ECS reads this via <see cref="SimControlSyncSystem"/>.
    /// </summary>
    public sealed class SimGate
    {
        public static SimGate Active { get; private set; }

        public SimRunMode Current { get; private set; } = SimRunMode.Off;

        public void BindAsActive()
        {
            Active = this;
        }

        public static void ClearActive()
        {
            Active = null;
        }

        public void Set(SimRunMode mode)
        {
            if (Current == mode)
                return;

            Current = mode;
            GameLog.Info(LogChannel.Bootstrap, $"SimGate → {mode}");
        }
    }
}
