using TheyWillDescend.Infrastructure.Logging;

namespace TheyWillDescend.Shell.States
{
    public sealed class PressAnyKeyState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly GameInput _input;

        public AppStateId Id => AppStateId.PressAnyKey;

        public PressAnyKeyState(AppStateMachine fsm, GameInput input)
        {
            _fsm = fsm;
            _input = input;
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
            _input.Proceeded += OnProceeded;
            _input.EnableMenu();
            GameLog.Info("Press any key to continue.");
        }

        public void Exit()
        {
            _input.Proceeded -= OnProceeded;
            _input.Disable();
        }

        public void Tick() { }

        void OnProceeded()
        {
            _fsm.TransitionTo(AppStateId.MainMenu);
        }
    }
}
