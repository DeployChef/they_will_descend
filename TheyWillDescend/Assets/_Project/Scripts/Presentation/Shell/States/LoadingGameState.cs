using TheyWillDescend.Infrastructure.Logging;

namespace TheyWillDescend.Shell.States
{
    public sealed class LoadingGameState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly SimGate _simGate;
        readonly GameSession _session;

        public AppStateId Id => AppStateId.LoadingGame;

        public LoadingGameState(AppStateMachine fsm, SimGate simGate, GameSession session)
        {
            _fsm = fsm;
            _simGate = simGate;
            _session = session;
        }

        public void Enter()
        {
            _simGate.SetSessionInGame(false);
            GameLog.Info("Loading game session…");
            _session.Start(() => _fsm.TransitionTo(AppStateId.Playing));
        }

        public void Exit() { }

        public void Tick() { }
    }
}
