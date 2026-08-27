using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.ShellUi;

namespace TheyWillDescend.Shell.States
{
    public sealed class MainMenuState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly GameInput _input;
        MainMenuScreen _screen;

        public AppStateId Id => AppStateId.MainMenu;

        public MainMenuState(AppStateMachine fsm, GameInput input)
        {
            _fsm = fsm;
            _input = input;
        }

        public void Enter()
        {
            _input.Disable();
            _screen = MainMenuScreen.Current;
            if (_screen == null)
            {
                GameLog.Error("MainMenuState: MainMenuScreen missing. Put it on MainMenuPanel in MainMenu.");
                return;
            }

            _screen.Show();
            _screen.StartClicked += OnStartClicked;
            GameLog.Info("Main menu: click Start Game.");
        }

        public void Exit()
        {
            if (_screen != null)
            {
                _screen.StartClicked -= OnStartClicked;
                _screen.Hide();
            }

            _screen = null;
        }

        void OnStartClicked()
        {
            _fsm.TransitionTo(AppStateId.LoadingGame);
        }
    }
}
