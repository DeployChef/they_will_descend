using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Presentation.ShellUi;
using _Project.Scripts.Simulation.Session;

namespace _Project.Scripts.Shell.States
{
    public sealed class PressAnyKeyState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly SimGate _simGate;
        readonly IShellIntentSource _intents;
        readonly ShellUiBinder _ui;

        public AppStateId Id => AppStateId.PressAnyKey;

        public PressAnyKeyState(
            AppStateMachine fsm,
            SimGate simGate,
            IShellIntentSource intents,
            ShellUiBinder ui)
        {
            _fsm = fsm;
            _simGate = simGate;
            _intents = intents;
            _ui = ui;
        }

        public void Enter()
        {
            _simGate.Set(SimRunMode.Off);
            _ui.ShowPressAnyKey();
            GameLog.Info(LogChannel.Bootstrap, "Press any key to continue.");
        }

        public void Exit() { }

        public void Tick()
        {
            if (_intents.ConsumeProceed())
                _fsm.TransitionTo(AppStateId.MainMenu);
        }
    }
}
