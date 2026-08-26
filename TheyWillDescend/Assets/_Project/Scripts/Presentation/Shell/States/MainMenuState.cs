using TheyWillDescend.Infrastructure.Logging;

namespace TheyWillDescend.Shell.States
{
    public sealed class MainMenuState : IAppState
    {
        readonly AppStateMachine _fsm;
        IShellUi _ui;

        public AppStateId Id => AppStateId.MainMenu;

        public MainMenuState(AppStateMachine fsm)
        {
            _fsm = fsm;
        }

        public void Enter()
        {
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

        public void Tick() { }

        void OnStartGameClicked()
        {
            _fsm.TransitionTo(AppStateId.LoadingGame);
        }
    }
}
