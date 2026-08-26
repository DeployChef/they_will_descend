using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Io;

namespace TheyWillDescend.Shell.States
{
    public sealed class LoadingGameState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly GameSession _session;

        public AppStateId Id => AppStateId.LoadingGame;

        public LoadingGameState(AppStateMachine fsm, GameSession session)
        {
            _fsm = fsm;
            _session = session;
        }

        public void Enter()
        {
            SimCommands.TryPost(SimClockCommand.InGame(false));
            GameLog.Info("Loading game session…");
            _session.Start(() => _fsm.TransitionTo(AppStateId.Playing));
        }

        public void Exit() { }

        public void Tick() { }
    }
}
