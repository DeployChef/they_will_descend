using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.App;
using TheyWillDescend.Content;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using UnityEngine;

namespace TheyWillDescend.Shell
{
    public enum RunKind
    {
        Normal = 0,
        Debug = 1
    }

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

        [Header("Run kits")]
        [SerializeField] ScenarioDefinition defaultScenario;
        [SerializeField] ScenarioDefinition debugScenario;
        [SerializeField] DifficultyProfile debugDifficulty;

        readonly SceneLoader _scenes = new();
        CancellationTokenSource _runCts;
        RunKind _kind;
        bool _loadSlot;

        public bool IsActive { get; private set; }

        public void SetRunKind(RunKind kind)
        {
            _kind = kind;
            _loadSlot = false;
        }

        public void SetLoadSlot() => _loadSlot = true;

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

            RunSnapshot snapshot = null;
            if (_loadSlot && !RunSnapshotStore.TryRead(out snapshot))
            {
                await _scenes.Unload(loadingScene, ct);
                return;
            }

            await _scenes.Unload(mainMenuScene, ct);
            await _scenes.LoadAdditive(gameScene, setActive: true, ct);

            if (!_scenes.IsLoaded(gameScene))
            {
                GameLog.Error("GameSession.Start failed — Game scene not loaded.");
                await AbortAsync(ct);
                return;
            }

            await WaitUntilSimulationReady(ct);
            if (ct.IsCancellationRequested)
                return;
            if (!IsSimulationReady())
            {
                GameLog.Error("GameSession.Start failed — simulation not ready.");
                await AbortAsync(ct);
                return;
            }

            if (_loadSlot)
            {
                RunPublisher.ResetDynamics();
                RunSessionSnapshot.Apply(snapshot);
                PauseMenuScreen.Current?.RebuildViews();
            }
            else
            {
                var debug = _kind == RunKind.Debug;
                var scenario = debug ? debugScenario : defaultScenario;
                var difficulty = debug ? debugDifficulty : null;
                if (debug && scenario == null)
                    GameLog.Error("GameSession: DebugScenario is not assigned on GameSession.");
                GameLog.Info(
                    $"Run kit: {(debug ? "Debug" : "Normal")} " +
                    $"scenario={(scenario != null ? scenario.name : "null")} " +
                    $"overlay={(difficulty != null ? difficulty.name : "stamp defaults")}.");
                if (!RunPublisher.Apply(scenario, difficulty))
                {
                    GameLog.Error("GameSession.Start failed — run publisher.");
                    await AbortAsync(ct);
                    return;
                }
            }

            await _scenes.Unload(loadingScene, ct);
            IsActive = true;
            _loadSlot = false;
        }

        public async UniTask RunWithLoadingAsync(
            Func<CancellationToken, UniTask> work,
            CancellationToken cancellationToken = default)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            await _scenes.LoadAdditive(loadingScene, setActive: false, cancellationToken);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            await UniTask.NextFrame(cancellationToken);
            try
            {
                await work(cancellationToken);
            }
            finally
            {
                await _scenes.Unload(loadingScene, cancellationToken);
            }
        }

        public async UniTask DisposeAsync(CancellationToken cancellationToken = default)
        {
            Cancel();
            RunPublisher.ResetDynamics();
            if (!_scenes.IsLoaded(loadingScene))
                await _scenes.LoadAdditive(loadingScene, setActive: false, cancellationToken);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            await UniTask.NextFrame(cancellationToken);
            await _scenes.Unload(gameScene, cancellationToken);
            await _scenes.LoadAdditive(mainMenuScene, setActive: false, cancellationToken);
            IsActive = false;
            _kind = RunKind.Normal;
            _loadSlot = false;
        }

        public async UniTask HideLoadingAsync(CancellationToken cancellationToken = default)
        {
            await _scenes.Unload(loadingScene, cancellationToken);
        }

        public void Cancel()
        {
            if (_runCts == null)
                return;
            _runCts.Cancel();
            _runCts.Dispose();
            _runCts = null;
        }

        async UniTask AbortAsync(CancellationToken cancellationToken)
        {
            RunPublisher.ResetDynamics();
            if (_scenes.IsLoaded(gameScene))
                await _scenes.Unload(gameScene, cancellationToken);
            if (!_scenes.IsLoaded(mainMenuScene))
                await _scenes.LoadAdditive(mainMenuScene, setActive: false, cancellationToken);
            await _scenes.Unload(loadingScene, cancellationToken);
            IsActive = false;
            _kind = RunKind.Normal;
            _loadSlot = false;
        }

        static bool IsSimulationReady()
        {
            if (!SimWorld.TryGet(out var em, out var session))
                return false;
            if (!em.HasComponent<CityGrid>(session)
                || em.GetComponentData<CityGrid>(session).Ready == 0)
                return false;
            if (!em.HasBuffer<BuildingPrototype>(session))
                return false;
            return em.GetBuffer<BuildingPrototype>(session).Length > 0;
        }

        void OnDestroy() => Cancel();

        async UniTask WaitUntilSimulationReady(CancellationToken cancellationToken)
        {
            var timeout = simulationReadyTimeoutSeconds > 0f ? simulationReadyTimeoutSeconds : 30f;
            try
            {
                await UniTask.WaitUntil(
                        IsSimulationReady,
                        cancellationToken: cancellationToken)
                    .Timeout(TimeSpan.FromSeconds(timeout));
            }
            catch (TimeoutException)
            {
                GameLog.Error(
                    "GameSession: catalog/grid never appeared (SubScene bake). " +
                    "Check Simulation SubScene is in Game and Building Catalog is assigned.");
            }
        }
    }
}
