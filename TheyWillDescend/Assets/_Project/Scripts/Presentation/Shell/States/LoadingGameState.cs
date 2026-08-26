using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Io;

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
            SimCommands.TryPost(SimClockCommand.InGame(false));
            GameLog.Info("Loading game session…");
            _session.Start(() => _fsm.TransitionTo(AppStateId.Playing));
        }

        public void Exit() { }

        public void Tick() { }
    }
}
