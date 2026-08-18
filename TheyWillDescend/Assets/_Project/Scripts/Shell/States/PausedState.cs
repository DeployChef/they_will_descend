using _Project.Scripts.Infrastructure.Logging;

namespace _Project.Scripts.Shell.States
{
    /// <summary>
    /// Kept registered for later pause-menu overlay. Time pause is <see cref="SimGate.TogglePlayerPause"/>
    /// while staying in <see cref="PlayingState"/>.
    /// </summary>
    public sealed class PausedState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly IShellIntentSource _intents;

        public AppStateId Id => AppStateId.Paused;

        public PausedState(AppStateMachine fsm, SimGate simGate, IShellIntentSource intents)
        {
            _fsm = fsm;
            _intents = intents;
        }

        public void Enter()
        {
            GameLog.Info("Paused app state (unused for clock; Esc uses SimGate).");
        }

        public void Exit() { }

        public void Tick()
        {
            if (_intents.ConsumePauseToggle())
                _fsm.TransitionTo(AppStateId.Playing);
        }
    }
}
