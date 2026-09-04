using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Shell;

namespace TheyWillDescend.Shell.States
{
    public sealed class MainMenuState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly GameInput _input;
        readonly GameSession _session;
        MainMenuScreen _screen;

        public AppStateId Id => AppStateId.MainMenu;

        public MainMenuState(AppStateMachine fsm, GameInput input, GameSession session)
        {
            _fsm = fsm;
            _input = input;
            _session = session;
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
            _screen.SetLoadEnabled(RunSnapshotStore.HasSlot);
            _screen.StartClicked += OnStartClicked;
            _screen.DebugClicked += OnDebugClicked;
            _screen.LoadClicked += OnLoadClicked;
            GameLog.Info("Main menu: Start Game, Load, or Start Debug.");
        }

        public void Exit()
        {
            if (_screen != null)
            {
                _screen.StartClicked -= OnStartClicked;
                _screen.DebugClicked -= OnDebugClicked;
                _screen.LoadClicked -= OnLoadClicked;
                _screen.Hide();
            }

            _screen = null;
        }

        void OnStartClicked()
        {
            _session.SetRunKind(RunKind.Normal);
            _fsm.TransitionTo(AppStateId.LoadingGame);
        }

        void OnDebugClicked()
        {
            GameLog.Info("Main menu: Start Debug.");
            _session.SetRunKind(RunKind.Debug);
            _fsm.TransitionTo(AppStateId.LoadingGame);
        }

        void OnLoadClicked()
        {
            if (!RunSnapshotStore.HasSlot)
            {
                GameLog.Warning("Main menu: no save slot.");
                _screen?.SetLoadEnabled(false);
                return;
            }

            GameLog.Info("Main menu: Load slot.");
            _session.SetLoadSlot();
            _fsm.TransitionTo(AppStateId.LoadingGame);
        }
    }
}
