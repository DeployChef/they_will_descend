using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Shell;

namespace TheyWillDescend.Shell.States
{
    public sealed class ReturningToMenuState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly GameSession _session;
        readonly GameInput _input;
        CancellationTokenSource _cts;

        public AppStateId Id => AppStateId.ReturningToMenu;

        public ReturningToMenuState(AppStateMachine fsm, GameSession session, GameInput input)
        {
            _fsm = fsm;
            _session = session;
            _input = input;
        }

        public void Enter()
        {
            _input.Disable();
            GameLog.Info("Returning to main menu…");
            _cts = new CancellationTokenSource();
            LeaveThenMenu(_cts.Token).Forget();
        }

        public void Exit()
        {
            if (_session.IsActive)
                _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        async UniTaskVoid LeaveThenMenu(CancellationToken cancellationToken)
        {
            try
            {
                await _session.DisposeAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    return;
                _fsm.TransitionTo(AppStateId.MainMenu);
                await _session.HideLoadingAsync();
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
