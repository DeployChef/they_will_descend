using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.ShellUi;

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
            var screen = PressAnyKeyScreen.Current;
            if (screen == null)
            {
                GameLog.Error("PressAnyKeyState: PressAnyKeyScreen missing. Put it on PressAnyKeyPanel in MainMenu.");
                return;
            }

            screen.Show();
            _input.Proceeded += OnProceeded;
            _input.EnableMenu();
            GameLog.Info("Press any key to continue.");
        }

        public void Exit()
        {
            _input.Proceeded -= OnProceeded;
            _input.Disable();
            PressAnyKeyScreen.Current?.Hide();
        }

        void OnProceeded()
        {
            _fsm.TransitionTo(AppStateId.MainMenu);
        }
    }
}
