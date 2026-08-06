using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Simulation.Session;

namespace _Project.Scripts.Shell.States
{
    public sealed class PausedState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly SimGate _simGate;
        readonly IShellIntentSource _intents;

        public AppStateId Id => AppStateId.Paused;

        public PausedState(AppStateMachine fsm, SimGate simGate, IShellIntentSource intents)
        {
            _fsm = fsm;
            _simGate = simGate;
            _intents = intents;
        }

        public void Enter()
        {
            _simGate.Set(SimRunMode.Frozen);
            GameLog.Info(LogChannel.Bootstrap, "Paused: Esc to resume.");
        }

        public void Exit() { }

        public void Tick()
        {
            if (_intents.ConsumePauseToggle())
                _fsm.TransitionTo(AppStateId.Playing);
        }
    }
}
