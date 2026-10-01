using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.App;
using TheyWillDescend.Content;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Simulation.Session;
using UnityEngine;
using VContainer;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// One run inside the Game scene. Catalogs and rules live on this object.
    /// <see cref="ShellService.EnterGame"/> loads the scene and calls <see cref="Begin"/>.
    /// This object does not load or unload scenes. <see cref="GameRun"/> turns the clock on.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        GameRun _run;
        SaveService _save;
        bool _restorePlay;
        [Header("Ready")]
        [SerializeField] float simulationReadyTimeoutSeconds = 30f;

        [Header("Run kits")]
        [SerializeField] ScenarioDefinition defaultScenario;
        [SerializeField] ScenarioDefinition debugScenario;
        [SerializeField] TechCatalogAsset[] techCatalogs;

        [Header("Catalogs & Rules")]
        [SerializeField] BuildingCatalogAsset buildingCatalog;
        [SerializeField] ResourceCatalogAsset resourceCatalog;
        [SerializeField] SimRulesAsset simRules;
        [SerializeField] TimelineCatalogAsset timelineCatalog;

        [Inject]
        public void Construct(GameRun run, SaveService save)
        {
            _run = run;
            _save = save;
        }

        public void StopPlay()
        {
            if (_run == null || !_run.IsLive)
                return;
            _restorePlay = true;
            _run.Disarm();
        }

        public async UniTask<bool> Begin(RunLaunch launch, CancellationToken cancellationToken = default)
        {
            EnsureDefaultAssets();
            var debug = !launch.LoadSlot && launch.Kind == RunKind.Debug;
            var scenario = debug ? debugScenario : defaultScenario;
            var storyPack = scenario != null ? scenario.StoryPack : null;
            if (storyPack == null && defaultScenario != null)
                storyPack = defaultScenario.StoryPack;
            if (SimWorld.TryGetEntityManager(out var em))
            {
                SimulationBootstrap.InitializeRun(
                    em,
                    buildingCatalog,
                    resourceCatalog,
                    simRules,
                    timelineCatalog,
                    storyPack);
            }

            if (!await WaitUntilSimulationReady(cancellationToken))
            {
                GameLog.Error("GameSession.Begin failed — simulation not ready.");
                return false;
            }

            if (launch.LoadSlot)
            {
                if (_save == null)
                {
                    GameLog.Error("GameSession.Begin failed — SaveService was not injected.");
                    return false;
                }

                if (!_save.TryRead(out var snapshot))
                {
                    GameLog.Error("GameSession.Begin failed — save slot is missing.");
                    return false;
                }

                if (!await Apply(snapshot, cancellationToken))
                    return false;
                ArmPlay();
                PauseMenuScreen.Current?.RebuildViews();
                return true;
            }

            if (debug && scenario == null)
                GameLog.Error("GameSession: DebugScenario is not assigned.");

            var difficulty = scenario != null ? scenario.DefaultDifficulty : null;
            GameLog.Info(
                $"Run kit: {(debug ? "Debug" : "Normal")} " +
                $"scenario={(scenario != null ? scenario.name : "null")} " +
                $"difficulty={(difficulty != null ? difficulty.name : "stamp defaults")}.");

            if (!RunPublisher.BeginRun(scenario, difficulty, techCatalogs)
                || !await WaitForPhaseAsync(SimSessionPhase.Ready, cancellationToken))
            {
                GameLog.Error("GameSession.Begin failed — ECS setup did not reach Ready.");
                return false;
            }

            ArmPlay();
            return true;
        }

        public async UniTask<bool> Apply(RunSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            if (!RunSessionSnapshot.BeginApply(snapshot, techCatalogs))
                return false;
            if (!await WaitForPhaseAsync(SimSessionPhase.Ready, cancellationToken))
            {
                GameLog.Error("GameSession.Apply failed — ECS setup did not reach Ready.");
                return false;
            }

            return true;
        }

        public async UniTask<bool> Shutdown(CancellationToken cancellationToken = default)
        {
            StopPlay();
            if (!SimWorld.TryGet(out _, out _))
            {
                _restorePlay = false;
                return true;
            }

            if (!RunPublisher.BeginReset()
                || !await WaitForPhaseAsync(SimSessionPhase.Unprepared, cancellationToken))
            {
                GameLog.Error("GameSession.Shutdown stopped — ECS reset was not confirmed.");
                if (_restorePlay)
                    ArmPlay();
                _restorePlay = false;
                return false;
            }

            _restorePlay = false;
            return true;
        }

        void ArmPlay()
        {
            if (_run == null)
            {
                GameLog.Error("GameSession: GameRun was not injected.");
                return;
            }

            _run.Arm();
            _restorePlay = false;
        }

        UniTask<bool> WaitForPhaseAsync(
            SimSessionPhase phase,
            CancellationToken cancellationToken)
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
                await UniTask.WaitUntil(condition, cancellationToken: timeoutCts.Token);
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

        static bool IsSimulationReady()
        {
            if (!SimWorld.TryGet(out var em, out var session))
                return false;
            if (!em.HasComponent<CityGrid>(session)
                || em.GetComponentData<CityGrid>(session).Ready == 0)
                return false;
            if (!em.HasBuffer<BaseBuildingPrototype>(session)
                || em.GetBuffer<BaseBuildingPrototype>(session).Length == 0)
                return false;
            if (!em.HasBuffer<BaseBuildingCatalogCost>(session)
                || !em.HasBuffer<BaseBuildingCatalogRecipe>(session)
                || !em.HasBuffer<BuildingPrototype>(session)
                || em.GetBuffer<BuildingPrototype>(session).Length == 0
                || !em.HasBuffer<BuildingCatalogCost>(session)
                || !em.HasBuffer<BuildingCatalogRecipe>(session))
                return false;
            if (!em.HasComponent<SimPrototypes>(session)
                || em.GetComponentData<SimPrototypes>(session).Agent == Unity.Entities.Entity.Null)
                return false;
            return SimSessionAccess.HasLifecycleQueues(em, session);
        }

        static bool IsSessionPhase(SimSessionPhase phase)
        {
            return SimWorld.TryGet(out var em, out var session)
                && em.GetComponentData<SimSession>(session).Phase == phase;
        }

        void Awake() => EnsureDefaultAssets();

        void EnsureDefaultAssets()
        {
#if UNITY_EDITOR
            if (buildingCatalog == null)
                buildingCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/_Project/Content/Buildings/DefaultBuildingCatalog.asset");
            if (resourceCatalog == null)
                resourceCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ResourceCatalogAsset>("Assets/_Project/Content/Economy/DefaultResourceCatalog.asset");
            if (simRules == null)
                simRules = UnityEditor.AssetDatabase.LoadAssetAtPath<SimRulesAsset>("Assets/_Project/Content/Rules/DefaultSimRules.asset");
            if (timelineCatalog == null)
                timelineCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<TimelineCatalogAsset>("Assets/_Project/Content/Timeline/DefaultTimeline.asset");
#endif
        }
    }
}
