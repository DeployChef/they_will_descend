using TheyWillDescend.Infrastructure.Logging;

namespace TheyWillDescend.Shell.States
{
    public sealed class MainMenuState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly GameInput _input;
        IShellUi _ui;

        public AppStateId Id => AppStateId.MainMenu;

        public MainMenuState(AppStateMachine fsm, GameInput input)
        {
            _fsm = fsm;
            _input = input;
        }

        public void Enter()
        {
            _input.Disable();
            _ui = ShellUiPort.Current;
            if (_ui == null)
            {
                GameLog.Error("MainMenuState: IShellUi not bound. ShellUiBinder must be on a loaded MainMenu.");
                return;
            }

            _ui.ShowMainMenu();
            _ui.StartGameClicked += OnStartGameClicked;
            GameLog.Info("Main menu: click Start Game.");
        }

        public void Exit()
        {
            if (_ui != null)
                _ui.StartGameClicked -= OnStartGameClicked;
            _ui = null;
        }

        void OnStartGameClicked()
        {
            _fsm.TransitionTo(AppStateId.LoadingGame);
        }
    }
}
