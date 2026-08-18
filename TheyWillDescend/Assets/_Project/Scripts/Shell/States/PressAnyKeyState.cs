using _Project.Scripts.Infrastructure.Logging;

namespace _Project.Scripts.Shell.States
{
    public sealed class PressAnyKeyState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly SimGate _simGate;
        readonly IShellIntentSource _intents;
        readonly IShellUi _ui;

        public AppStateId Id => AppStateId.PressAnyKey;

        public PressAnyKeyState(
            AppStateMachine fsm,
            SimGate simGate,
            IShellIntentSource intents,
            IShellUi ui)
        {
            _fsm = fsm;
            _simGate = simGate;
            _intents = intents;
            _ui = ui;
        }

        public void Enter()
        {
            _simGate.SetSessionInGame(false);
            _ui.ShowPressAnyKey();
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
