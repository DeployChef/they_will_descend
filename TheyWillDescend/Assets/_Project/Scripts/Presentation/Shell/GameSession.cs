using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.App;
using TheyWillDescend.Content;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Simulation.Agents;
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

            if (!await WaitUntilSimulationReady(ct))
            {
                GameLog.Error("GameSession.Start failed — simulation not ready.");
                await AbortAsync(CancellationToken.None);
                return;
            }

            var setupBegan = false;
            if (_loadSlot)
            {
                setupBegan = RunSessionSnapshot.BeginApply(snapshot);
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
                setupBegan = RunPublisher.BeginRun(scenario, difficulty);
            }

            if (!setupBegan || !await WaitForPhaseAsync(SimSessionPhase.Ready, ct))
            {
                GameLog.Error("GameSession.Start failed — ECS setup did not reach Ready.");
                await AbortAsync(CancellationToken.None);
                return;
            }

            if (_loadSlot)
                PauseMenuScreen.Current?.RebuildViews();
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
                await _scenes.Unload(loadingScene, CancellationToken.None);
            }
        }

        public async UniTask DisposeAsync(CancellationToken cancellationToken = default)
        {
            Cancel();
            if (!_scenes.IsLoaded(loadingScene))
                await _scenes.LoadAdditive(loadingScene, setActive: false, cancellationToken);

            if (_scenes.IsLoaded(gameScene))
            {
                if (!RunPublisher.BeginReset()
                    || !await WaitForPhaseAsync(SimSessionPhase.Unprepared, cancellationToken))
                {
                    GameLog.Error("GameSession.Dispose stopped — ECS reset was not confirmed.");
                    return;
                }
            }

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
            if (_scenes.IsLoaded(gameScene))
            {
                if (!RunPublisher.BeginReset()
                    || !await WaitForPhaseAsync(SimSessionPhase.Unprepared, cancellationToken))
                {
                    GameLog.Error("GameSession.Abort stopped — ECS reset was not confirmed.");
                    return;
                }
                await _scenes.Unload(gameScene, cancellationToken);
            }
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
            if (em.GetBuffer<BuildingPrototype>(session).Length == 0)
                return false;
            if (!em.HasComponent<SimPrototypes>(session)
                || em.GetComponentData<SimPrototypes>(session).Agent == Unity.Entities.Entity.Null)
                return false;
            return SimSessionAccess.HasLifecycleQueues(em, session);
        }

        void OnDestroy() => Cancel();

        public UniTask<bool> WaitForPhaseAsync(
            SimSessionPhase phase,
            CancellationToken cancellationToken = default)
        {
            return AwaitCondition(
                () => IsSessionPhase(phase),
                $"ECS session did not reach {phase}.",
                cancellationToken);
        }

        UniTask<bool> WaitUntilSimulationReady(CancellationToken cancellationToken)
        {
            return AwaitCondition(
                IsSimulationReady,
                "Catalog/grid never appeared (SubScene bake). Check Simulation SubScene is in Game and Building Catalog is assigned.",
                cancellationToken);
        }

        async UniTask<bool> AwaitCondition(
            Func<bool> condition,
            string timeoutMessage,
            CancellationToken cancellationToken)
        {
            var timeout = simulationReadyTimeoutSeconds > 0f ? simulationReadyTimeoutSeconds : 30f;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeout));
            try
            {
                await UniTask.WaitUntil(
                        condition,
                        cancellationToken: timeoutCts.Token);
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                GameLog.Error($"GameSession: {timeoutMessage}");
                return false;
            }
            catch (OperationCanceledException)
            {
                GameLog.Info("GameSession: lifecycle wait cancelled.");
                return false;
            }
        }

        static bool IsSessionPhase(SimSessionPhase phase)
        {
            return SimWorld.TryGet(out var em, out var session)
                && em.GetComponentData<SimSession>(session).Phase == phase;
        }
    }
}
