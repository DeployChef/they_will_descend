using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.ShellUi;
using UnityEngine.SceneManagement;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// The only scene loader. Every transition is the same pipeline:
    /// show screen → teardown current scene → Single load → wait <see cref="LoadingTasks"/> → hide.
    /// A failed load goes back to the menu instead of leaving the screen up.
    /// </summary>
    public sealed class TransitionService : IDisposable
    {
        readonly LoadingScreen _screen;
        readonly LoadingTasks _loading;
        readonly List<Func<CancellationToken, UniTask<bool>>> _teardowns = new();
        CancellationTokenSource _life = new();
        bool _running;

        public TransitionService(LoadingScreen screen, LoadingTasks loading)
        {
            _screen = screen;
            _loading = loading;
            _loading.WorkAdded += OnWorkAdded;
        }

        public bool IsRunning => _running;

        public void Dispose()
        {
            _loading.WorkAdded -= OnWorkAdded;
            if (_life == null)
                return;
            _life.Cancel();
            _life.Dispose();
            _life = null;
        }

        /// <summary>
        /// The current scene's cleanup, run under the screen before it unloads.
        /// Returning false aborts the transition. Dispose the handle when the scene goes away.
        /// </summary>
        public IDisposable RegisterTeardown(Func<CancellationToken, UniTask<bool>> teardown)
        {
            _teardowns.Add(teardown);
            return new Registration(this, teardown);
        }

        public async UniTask<bool> Run(string sceneName)
        {
            if (_running || _life == null)
            {
                GameLog.Warning($"Transition to '{sceneName}' ignored: another transition is running.");
                return false;
            }

            _running = true;
            var ct = _life.Token;
            try
            {
                await _screen.Show(ct);
                if (!await RunTeardowns(ct))
                {
                    await _screen.Hide(ct);
                    return false;
                }

                if (!await LoadSingle(sceneName, ct))
                {
                    await _screen.Hide(ct);
                    return false;
                }

                if (await _loading.WaitAll(_screen.SetProgress, ct))
                {
                    await _screen.Hide(ct);
                    return true;
                }

                await FallBackToMenu(sceneName, ct);
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                _running = false;
            }
        }

        /// <summary>A scene opened directly asked for time with no transition running.</summary>
        void OnWorkAdded()
        {
            if (_running || _life == null)
                return;
            RunStandalone(_life.Token).Forget();
        }

        async UniTaskVoid RunStandalone(CancellationToken ct)
        {
            _running = true;
            try
            {
                _screen.ShowImmediate();
                if (await _loading.WaitAll(_screen.SetProgress, ct))
                {
                    await _screen.Hide(ct);
                    return;
                }

                await FallBackToMenu(SceneManager.GetActiveScene().name, ct);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _running = false;
            }
        }

        async UniTask FallBackToMenu(string failedScene, CancellationToken ct)
        {
            if (failedScene == GameScenes.MainMenu)
            {
                GameLog.Error("Transition: MainMenu failed to load its tasks. Leaving the screen down.");
                await _screen.Hide(ct);
                return;
            }

            GameLog.Error($"Transition: '{failedScene}' failed to start. Returning to MainMenu.");
            if (!await RunTeardowns(ct))
                GameLog.Error("Transition: teardown after a failed start was not confirmed.");
            await LoadSingle(GameScenes.MainMenu, ct);
            await _loading.WaitAll(_screen.SetProgress, ct);
            await _screen.Hide(ct);
        }

        async UniTask<bool> RunTeardowns(CancellationToken ct)
        {
            var snapshot = _teardowns.ToArray();
            for (var i = 0; i < snapshot.Length; i++)
            {
                if (!await snapshot[i](ct))
                {
                    GameLog.Error("Transition aborted: scene teardown was not confirmed.");
                    return false;
                }
            }

            return true;
        }

        static async UniTask<bool> LoadSingle(string sceneName, CancellationToken ct)
        {
            GameLog.Info($"Loading '{sceneName}'…");
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                GameLog.Error($"Failed to load '{sceneName}'. Add it to Build Settings.");
                return false;
            }

            await op.ToUniTask(cancellationToken: ct);
            // Scenes may also register work in Start, which runs on the next frame.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            GameLog.Info($"Scene '{sceneName}' loaded.");
            return true;
        }

        sealed class Registration : IDisposable
        {
            TransitionService _owner;
            readonly Func<CancellationToken, UniTask<bool>> _teardown;

            public Registration(TransitionService owner, Func<CancellationToken, UniTask<bool>> teardown)
            {
                _owner = owner;
                _teardown = teardown;
            }

            public void Dispose()
            {
                _owner?._teardowns.Remove(_teardown);
                _owner = null;
            }
        }
    }
}
