using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Simulation.Session;

namespace _Project.Scripts.Shell.States
{
    public sealed class PlayingState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly SimGate _simGate;
        readonly IShellIntentSource _intents;

        public AppStateId Id => AppStateId.Playing;

        public PlayingState(AppStateMachine fsm, SimGate simGate, IShellIntentSource intents)
        {
            _fsm = fsm;
            _simGate = simGate;
            _intents = intents;
        }

        public void Enter()
        {
            _simGate.Set(SimRunMode.Running);
            GameLog.Info(LogChannel.Bootstrap, "Playing: Esc to pause.");
        }

        public void Exit() { }

        public void Tick()
        {
            if (_intents.ConsumePauseToggle())
                _fsm.TransitionTo(AppStateId.Paused);
        }
    }
}
