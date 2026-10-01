using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Switches presentation scenes and starts a run once Game is loaded.
    /// <see cref="ReturnToMenu"/> ends that run and unloads Game before the menu appears.
    /// The launch is an argument of <see cref="EnterGame"/>, not app context.
    /// </summary>
    public sealed class ShellService : IDisposable
    {
        readonly SceneLoader _scenes = new();
        readonly AppContext _context;
        CancellationTokenSource _life = new();
        CancellationTokenSource _op;
        bool _busy;

        public ShellService(AppContext context)
        {
            _context = context;
        }

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

        public void EnterGame(RunLaunch launch)
        {
            if (_busy)
                return;
            _busy = true;
            _context.IsFirstStart = true;
            Enter(launch).Forget();
        }

        public async UniTask<bool> ReturnToMenu()
        {
            if (_busy)
                return false;
            _busy = true;
            BeginOp();
            var ct = _op.Token;
            try
            {
                await ShowLoading(ct);

                var session = ResolveFrom<GameSession>(GameScenes.Game);
                if (session != null && !await session.Shutdown(ct))
                {
                    await HideLoading(ct);
                    return false;
                }

                await _scenes.Unload(GameScenes.Game, ct);
                await OpenMainMenu(ct);
                await HideLoading(ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                _busy = false;
            }
        }

        async UniTaskVoid Enter(RunLaunch launch)
        {
            BeginOp();
            var ct = _op.Token;
            try
            {
                await ShowLoading(ct);
                await _scenes.Unload(GameScenes.MainMenu, ct);
                await _scenes.LoadAdditive(GameScenes.Game, setActive: true, ct);

                var session = ResolveFrom<GameSession>(GameScenes.Game);
                if (session != null && await session.Begin(launch, ct))
                {
                    await HideLoading(ct);
                    return;
                }

                if (session != null)
                    await session.Shutdown(ct);
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

        static T ResolveFrom<T>(string sceneName) where T : class
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var scope = roots[i].GetComponentInChildren<LifetimeScope>(true);
                if (scope == null || scope.Container == null)
                    continue;
                return scope.Container.Resolve<T>();
            }

            return null;
        }
    }
}
