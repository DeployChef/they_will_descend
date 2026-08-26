using TheyWillDescend.Infrastructure.Logging;

namespace TheyWillDescend.Shell.States
{
    public sealed class LoadingGameState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly GameSession _session;
        readonly GameInput _input;

        public AppStateId Id => AppStateId.LoadingGame;

        public LoadingGameState(AppStateMachine fsm, GameSession session, GameInput input)
        {
            _fsm = fsm;
            _session = session;
            _input = input;
        }

        public void Enter()
        {
            _input.Disable();
            GameLog.Info("Loading game session…");
            _session.Start(() => _fsm.TransitionTo(AppStateId.Playing));
        }

        public void Exit() { }
    }
}
