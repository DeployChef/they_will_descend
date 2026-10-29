using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Presentation.Infrastructure;
using TheyWillDescend.Presentation.ShellUi;
using UnityEngine.SceneManagement;

namespace TheyWillDescend.Presentation.Shell
{
    /// <summary>
    /// Where the player goes next, and the load that gets them there.
    /// Show screen → teardown → Single load → wait for the screen's tasks → hide.
    /// Writes <see cref="AppContext.CurrentLaunch"/> on start and restart. Does not know <see cref="GameSession"/>.
    /// </summary>
    public sealed class ShellService : IDisposable
    {
        readonly AppContext _app;
        readonly LoadingScreen _screen;
        readonly List<Func<CancellationToken, UniTask<bool>>> _teardowns = new();
        CancellationTokenSource _life = new();
        bool _running;

        public ShellService(AppContext app, LoadingScreen screen)
        {
            _app = app;
            _screen = screen;
            _screen.WorkAdded += OnWorkAdded;
        }

        public void Dispose()
        {
            _screen.WorkAdded -= OnWorkAdded;
            if (_life == null)
                return;
            _life.Cancel();
            _life.Dispose();
            _life = null;
        }

        public UniTask<bool> StartRun(RunLaunch launch) => ChangeScene(GameScenes.Game, launch);

        public UniTask<bool> RestartRun(RunLaunch launch) => ChangeScene(GameScenes.Game, launch);

        public UniTask<bool> GoToMenu() => ChangeScene(GameScenes.MainMenu);

        /// <summary>
        /// The current scene's cleanup, run under the screen before it unloads.
        /// Returning false aborts the transition. Dispose the handle when the scene goes away.
        /// </summary>
        public IDisposable RegisterTeardown(Func<CancellationToken, UniTask<bool>> teardown)
        {
            _teardowns.Add(teardown);
            return new Registration(this, teardown);
        }

        async UniTask<bool> ChangeScene(string sceneName, RunLaunch? launch = null)
        {
            if (_running || _life == null)
            {
                GameLog.Warning("Shell: transition ignored, another one is running.");
                return false;
            }

            if (launch.HasValue)
                _app.CurrentLaunch = launch.Value;

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

                await LoadSingle(sceneName, ct);
                await FinishLoading(ct);
                return true;
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
                await FinishLoading(ct);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _running = false;
            }
        }

        async UniTask FinishLoading(CancellationToken ct)
        {
            await _screen.Wait(ct);
            await _screen.Hide(ct);
        }

        async UniTask<bool> RunTeardowns(CancellationToken ct)
        {
            var snapshot = _teardowns.ToArray();
            for (var i = 0; i < snapshot.Length; i++)
            {
                if (!await snapshot[i](ct))
                {
                    GameLog.Error("Shell: scene teardown was not confirmed.");
                    return false;
                }
            }

            return true;
        }

        static async UniTask LoadSingle(string sceneName, CancellationToken ct)
        {
            GameLog.Info($"Loading '{sceneName}'…");
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
                throw new InvalidOperationException($"Failed to load '{sceneName}'. Add it to Build Settings.");

            await op.ToUniTask(cancellationToken: ct);
            // Scenes may also register work in Start, which runs on the next frame.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            GameLog.Info($"Scene '{sceneName}' loaded.");
        }

        sealed class Registration : IDisposable
        {
            ShellService _owner;
            readonly Func<CancellationToken, UniTask<bool>> _teardown;

            public Registration(ShellService owner, Func<CancellationToken, UniTask<bool>> teardown)
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
