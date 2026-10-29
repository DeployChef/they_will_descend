using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Content;
using TheyWillDescend.Presentation.Infrastructure;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Presentation.SimBridge;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using UnityEngine;
using VContainer;

namespace TheyWillDescend.Presentation.Shell
{
    /// <summary>
    /// One run inside the Game scene. Catalogs and rules live on this object.
    /// <see cref="GameEntry"/> calls <see cref="Begin"/> as loading work and registers <see cref="Shutdown"/> as teardown.
    /// Begin/Shutdown are synchronous on the ECS world.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        SaveService _save;

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
        public void Construct(SaveService save)
        {
            _save = save;
        }

        public UniTask<bool> Begin(RunLaunch launch, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureDefaultAssets();
            if (!SimWorld.TryGetEntityManager(out var em))
            {
                GameLog.Error("GameSession.Begin failed — ECS world is missing.");
                return UniTask.FromResult(false);
            }

            var world = World.DefaultGameObjectInjectionWorld;
            var init = world != null ? world.GetExistingSystemManaged<InitializationSystemGroup>() : null;
            var sim = world != null ? world.GetExistingSystemManaged<SimulationSystemGroup>() : null;
            var initWasEnabled = init != null && init.Enabled;
            var simWasEnabled = sim != null && sim.Enabled;
            if (init != null)
                init.Enabled = false;
            if (sim != null)
                sim.Enabled = false;

            try
            {
                em.CompleteAllTrackedJobs();
                RunWorld.Wipe(em);

            var debug = !launch.IsSave && launch.Debug;
            var scenario = debug ? debugScenario : defaultScenario;
            var storyPack = scenario != null ? scenario.StoryPack : null;
            if (storyPack == null && defaultScenario != null)
                storyPack = defaultScenario.StoryPack;

            var workerSpeed = simRules != null ? simRules.WorkerSpeed : 2f;

            if (launch.IsSave)
            {
                if (_save == null)
                {
                    GameLog.Error("GameSession.Begin failed — SaveService was not injected.");
                    return UniTask.FromResult(false);
                }

                if (!_save.TryRead(launch.SaveId, out var snapshot))
                {
                    GameLog.Error($"GameSession.Begin failed — save '{launch.SaveId}' is missing.");
                    return UniTask.FromResult(false);
                }

                var session = SimulationBootstrap.InitializeRun(
                    em,
                    buildingCatalog,
                    resourceCatalog,
                    simRules,
                    timelineCatalog,
                    storyPack,
                    null,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _);
                if (session == Entity.Null || !SnapshotRestore.Apply(em, snapshot, techCatalogs, workerSpeed))
                    return UniTask.FromResult(false);

                PauseMenuScreen.Current?.RebuildViews();
                return UniTask.FromResult(true);
            }

            if (debug && scenario == null)
                GameLog.Error("GameSession: DebugScenario is not assigned.");

            DifficultyProfile difficulty = null;
            if (scenario != null)
            {
                var requested = scenario.DefaultDifficulty;
                if (!scenario.TryResolveDifficulty(requested, out difficulty))
                {
                    GameLog.Error(
                        $"GameSession: difficulty {requested.name} is not allowed by scenario {scenario.name}.");
                    return UniTask.FromResult(false);
                }
            }

            GameLog.Info(
                $"Run kit: {(debug ? "Debug" : "Normal")} " +
                $"scenario={(scenario != null ? scenario.name : "null")} " +
                $"difficulty={(difficulty != null ? difficulty.name : "stamp defaults")}.");

            var seeded = SimulationBootstrap.InitializeRun(
                em,
                buildingCatalog,
                resourceCatalog,
                simRules,
                timelineCatalog,
                storyPack,
                difficulty,
                out var catalog,
                out var recipes,
                out var costs,
                out var stock,
                out var info);
            if (seeded == Entity.Null)
            {
                GameLog.Error("GameSession.Begin failed — bootstrap did not create a session.");
                return UniTask.FromResult(false);
            }

            if (!ScenarioSetup.Apply(em, seeded, scenario, techCatalogs, workerSpeed, catalog, recipes, costs, stock, info))
            {
                GameLog.Error("GameSession.Begin failed — scenario seed failed.");
                return UniTask.FromResult(false);
            }

            return UniTask.FromResult(true);
            }
            catch (Exception e)
            {
                GameLog.Error($"GameSession.Begin failed — {e}");
                return UniTask.FromResult(false);
            }
            finally
            {
                if (init != null)
                    init.Enabled = initWasEnabled;
                if (sim != null)
                    sim.Enabled = simWasEnabled;
            }
        }

        public UniTask<bool> Shutdown(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!SimWorld.TryGetEntityManager(out var em))
                return UniTask.FromResult(true);

            var world = World.DefaultGameObjectInjectionWorld;
            var sim = world != null ? world.GetExistingSystemManaged<SimulationSystemGroup>() : null;
            if (sim != null)
                sim.Enabled = false;
            try
            {
                em.CompleteAllTrackedJobs();
                RunWorld.Wipe(em);
            }
            finally
            {
                if (sim != null)
                    sim.Enabled = true;
            }

            return UniTask.FromResult(true);
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
