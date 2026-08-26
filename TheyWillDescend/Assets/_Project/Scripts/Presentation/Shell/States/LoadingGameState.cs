using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Shell;

namespace TheyWillDescend.Shell.States
{
    public sealed class LoadingGameState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly GameSession _session;
        readonly GameInput _input;
        CancellationTokenSource _loadCts;

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
            _loadCts = new CancellationTokenSource();
            LoadThenPlay(_loadCts.Token).Forget();
        }

        public void Exit()
        {
            if (!_session.IsActive)
                _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;
        }

        async UniTaskVoid LoadThenPlay(CancellationToken cancellationToken)
        {
            try
            {
                await _session.StartAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested || !_session.IsActive)
                    return;
                _fsm.TransitionTo(AppStateId.Playing);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
