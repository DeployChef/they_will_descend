using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Io;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// One gameplay run: show Loading, load Game, wait until simulation exists, then hide Loading.
    /// Not a scene object — it owns the lifetime of Game, so it cannot live on Game.
    /// After <see cref="DisposeAsync"/>, MainMenu is back; caller should TransitionTo MainMenu.
    /// </summary>
    public sealed class GameSession
    {
        const float SimulationReadyTimeoutSeconds = 30f;

        readonly SceneLoader _scenes;
        CancellationTokenSource _runCts;

        public bool IsActive { get; private set; }

        public GameSession(SceneLoader scenes)
        {
            _scenes = scenes;
        }

        public async UniTask StartAsync(CancellationToken cancellationToken = default)
        {
            if (IsActive)
                return;

            Cancel();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var ct = _runCts.Token;

            await _scenes.LoadLoadingAdditive(ct);
            await _scenes.UnloadMainMenu(ct);
            await _scenes.LoadGameAdditive(ct);

            if (!_scenes.IsGameLoaded)
            {
                GameLog.Error("GameSession.Start failed — Game scene not loaded.");
                return;
            }

            await WaitUntilSimulationReady(ct);
            await _scenes.UnloadLoading(ct);
            IsActive = true;
        }

        public async UniTask DisposeAsync(CancellationToken cancellationToken = default)
        {
            Cancel();
            await _scenes.UnloadLoading(cancellationToken);
            await _scenes.UnloadGame(cancellationToken);
            await _scenes.LoadMainMenuAdditive(cancellationToken);
            IsActive = false;
        }

        public void Cancel()
        {
            if (_runCts == null)
                return;
            _runCts.Cancel();
            _runCts.Dispose();
            _runCts = null;
        }

        static async UniTask WaitUntilSimulationReady(CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.WaitUntil(
                        () => SimWorld.TryGet(out _, out _),
                        cancellationToken: cancellationToken)
                    .Timeout(TimeSpan.FromSeconds(SimulationReadyTimeoutSeconds));
            }
            catch (TimeoutException)
            {
                GameLog.Error(
                    "GameSession: SimControl never appeared (SubScene bake). " +
                    "Check Simulation SubScene is in Game and in Play Mode.");
            }
        }
    }
}
