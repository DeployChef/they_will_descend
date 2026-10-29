using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Presentation.Infrastructure;
using TheyWillDescend.Presentation.ShellUi;
using UnityEngine.SceneManagement;

namespace TheyWillDescend.Presentation.Shell
{
    /// <summary>
    /// Where the player goes next, and the load that gets them there.
    /// Show screen → Single load → wait for the screen's tasks → hide.
    /// Writes <see cref="AppContext.CurrentLaunch"/> on start and restart.
    /// The run itself lives and dies with the Game scene.
    /// </summary>
    public sealed class ShellService : IDisposable
    {
        readonly AppContext _app;
        readonly LoadingScreen _screen;
        CancellationTokenSource _life = new();
        bool _running;

        public ShellService(AppContext app, LoadingScreen screen)
        {
            _app = app;
            _screen = screen;
        }

        public void Dispose()
        {
            if (_life == null)
                return;
            _life.Cancel();
            _life.Dispose();
            _life = null;
        }

        public UniTask<bool> StartRun(RunLaunch launch) => ChangeScene(GameScenes.Game, launch);

        public UniTask<bool> RestartRun(RunLaunch launch) => ChangeScene(GameScenes.Game, launch);

        public UniTask<bool> GoToMenu() => ChangeScene(GameScenes.MainMenu);

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

        async UniTask FinishLoading(CancellationToken ct)
        {
            await _screen.Wait(ct);
            await _screen.Hide(ct);
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
    }
}
