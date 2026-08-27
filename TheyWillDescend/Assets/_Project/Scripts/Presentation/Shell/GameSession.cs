using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Io;
using UnityEngine;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Bootstrap host for one gameplay run. Scene names and bake timeout live here;
    /// <see cref="SceneLoader"/> only load/unload.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        [Header("Scenes")]
        [SerializeField] string loadingScene = GameScenes.Loading;
        [SerializeField] string gameScene = GameScenes.Game;
        [SerializeField] string mainMenuScene = GameScenes.MainMenu;

        [Header("Ready")]
        [SerializeField] float simulationReadyTimeoutSeconds = 30f;

        readonly SceneLoader _scenes = new();
        CancellationTokenSource _runCts;

        public bool IsActive { get; private set; }

        public async UniTask LoadMainMenuAsync(CancellationToken cancellationToken = default)
        {
            await _scenes.LoadAdditive(mainMenuScene, setActive: false, cancellationToken);
        }

        public async UniTask StartAsync(CancellationToken cancellationToken = default)
        {
            if (IsActive)
                return;

            Cancel();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var ct = _runCts.Token;

            await _scenes.LoadAdditive(loadingScene, setActive: false, ct);
            await _scenes.Unload(mainMenuScene, ct);
            await _scenes.LoadAdditive(gameScene, setActive: true, ct);

            if (!_scenes.IsLoaded(gameScene))
            {
                GameLog.Error("GameSession.Start failed — Game scene not loaded.");
                return;
            }

            await WaitUntilSimulationReady(ct);
            await _scenes.Unload(loadingScene, ct);
            IsActive = true;
        }

        public async UniTask DisposeAsync(CancellationToken cancellationToken = default)
        {
            Cancel();
            await _scenes.Unload(loadingScene, cancellationToken);
            await _scenes.Unload(gameScene, cancellationToken);
            await _scenes.LoadAdditive(mainMenuScene, setActive: false, cancellationToken);
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

        void OnDestroy() => Cancel();

        async UniTask WaitUntilSimulationReady(CancellationToken cancellationToken)
        {
            var timeout = simulationReadyTimeoutSeconds > 0f ? simulationReadyTimeoutSeconds : 30f;
            try
            {
                await UniTask.WaitUntil(
                        () => SimWorld.TryGet(out _, out _),
                        cancellationToken: cancellationToken)
                    .Timeout(TimeSpan.FromSeconds(timeout));
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
