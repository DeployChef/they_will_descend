using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Presentation.ShellUi;
using _Project.Scripts.Simulation.Session;

namespace _Project.Scripts.Shell.States
{
    public sealed class MainMenuState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly SimGate _simGate;
        readonly ShellUiBinder _ui;

        public AppStateId Id => AppStateId.MainMenu;

        public MainMenuState(AppStateMachine fsm, SimGate simGate, ShellUiBinder ui)
        {
            _fsm = fsm;
            _simGate = simGate;
            _ui = ui;
        }

        public void Enter()
        {
            _simGate.Set(SimRunMode.Off);
            _ui.ShowMainMenu();
            _ui.StartGameClicked += OnStartGameClicked;
            GameLog.Info(LogChannel.Bootstrap, "Main menu: click Start Game.");
        }

        public void Exit()
        {
            _ui.StartGameClicked -= OnStartGameClicked;
        }

        public void Tick() { }

        void OnStartGameClicked()
        {
            _fsm.TransitionTo(AppStateId.Playing);
        }
    }
}
