using TheyWillDescend.Infrastructure.Logging;

namespace TheyWillDescend.Shell.States
{
    public sealed class PressAnyKeyState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly SimGate _simGate;
        readonly IShellIntentSource _intents;

        public AppStateId Id => AppStateId.PressAnyKey;

        public PressAnyKeyState(
            AppStateMachine fsm,
            SimGate simGate,
            IShellIntentSource intents)
        {
            _fsm = fsm;
            _simGate = simGate;
            _intents = intents;
        }

        public void Enter()
        {
            _simGate.SetSessionInGame(false);
            var ui = ShellUiPort.Current;
            if (ui == null)
            {
                GameLog.Error("PressAnyKeyState: IShellUi not bound. ShellUiBinder must be on a loaded MainMenu.");
                return;
            }

            ui.ShowPressAnyKey();
            GameLog.Info("Press any key to continue.");
        }

        public void Exit() { }

        public void Tick()
        {
            if (_intents.ConsumeProceed())
                _fsm.TransitionTo(AppStateId.MainMenu);
        }
    }
}
