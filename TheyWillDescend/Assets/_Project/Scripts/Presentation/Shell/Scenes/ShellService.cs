using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Switches presentation scenes. Does not start a run, read saves, or touch the sim clock.
    /// The Game scene boots itself after it loads. Loading stays up until that scene dismisses it.
    /// </summary>
    public sealed class ShellService : IDisposable
    {
        readonly SceneLoader _scenes = new();
        CancellationTokenSource _life = new();
        CancellationTokenSource _op;
        bool _busy;

        public void Dispose()
        {
            CancelOp();
            if (_life == null)
                return;
            _life.Cancel();
            _life.Dispose();
            _life = null;
        }

        public UniTask OpenMainMenu(CancellationToken cancellationToken = default)
        {
            return _scenes.LoadAdditive(GameScenes.MainMenu, setActive: false, cancellationToken);
        }

        public UniTask ShowLoading(CancellationToken cancellationToken = default)
        {
            return _scenes.LoadAdditive(GameScenes.Loading, setActive: false, cancellationToken);
        }

        public UniTask HideLoading(CancellationToken cancellationToken = default)
        {
            return _scenes.Unload(GameScenes.Loading, cancellationToken);
        }

        public void EnterGame()
        {
            if (_busy)
                return;
            _busy = true;
            Enter().Forget();
        }

        public void ReturnToMenu()
        {
            if (_busy)
                return;
            _busy = true;
            Leave().Forget();
        }

        async UniTaskVoid Enter()
        {
            BeginOp();
            var ct = _op.Token;
            try
            {
                await ShowLoading(ct);
                await _scenes.Unload(GameScenes.MainMenu, ct);
                await _scenes.LoadAdditive(GameScenes.Game, setActive: true, ct);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _busy = false;
            }
        }

        async UniTaskVoid Leave()
        {
            BeginOp();
            var ct = _op.Token;
            try
            {
                await ShowLoading(ct);
                await _scenes.Unload(GameScenes.Game, ct);
                await OpenMainMenu(ct);
                await HideLoading(ct);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _busy = false;
            }
        }

        void BeginOp()
        {
            if (_life == null)
                _life = new CancellationTokenSource();
            CancelOp();
            _op = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        }

        void CancelOp()
        {
            if (_op == null)
                return;
            _op.Cancel();
            _op.Dispose();
            _op = null;
        }
    }
}
