using TheyWillDescend.Infrastructure.Logging;

namespace TheyWillDescend.Shell.States
{
    public sealed class PressAnyKeyState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly IShellIntentSource _intents;

        public AppStateId Id => AppStateId.PressAnyKey;

        public PressAnyKeyState(AppStateMachine fsm, IShellIntentSource intents)
        {
            _fsm = fsm;
            _intents = intents;
        }

        public void Enter()
        {
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
