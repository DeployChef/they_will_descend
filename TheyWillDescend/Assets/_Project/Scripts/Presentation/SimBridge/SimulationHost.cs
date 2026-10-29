using System;
using TheyWillDescend.Content;
using TheyWillDescend.Presentation.Infrastructure;
using TheyWillDescend.Presentation.Shell;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using UnityEngine;
using VContainer;

namespace TheyWillDescend.Presentation.SimBridge
{
    /// <summary>
    /// One run of the simulation while the Game scene lives.
    /// <see cref="Open"/> builds the session. <see cref="Dispose"/> deletes it.
    /// Systems are already in the default world; this object only owns the run's entities.
    /// </summary>
    public sealed class SimulationHost : MonoBehaviour, IDisposable
    {
        SaveService _save;
        bool _disposed;

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

        public bool Open(RunLaunch launch)
        {
            EnsureDefaultAssets();
            if (!SimWorld.TryGetEntityManager(out var em))
            {
                GameLog.Error("SimulationHost.Open failed — ECS world is missing.");
                return false;
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
                return launch.IsSave ? Restore(em, launch) : Seed(em, launch);
            }
            catch (Exception e)
            {
                GameLog.Error($"SimulationHost.Open failed — {e}");
                return false;
            }
            finally
            {
                if (init != null)
                    init.Enabled = initWasEnabled;
                if (sim != null)
                    sim.Enabled = simWasEnabled;
            }
        }

        public void Dispose() => Clear();

        void OnDestroy()
        {
            if (Application.isPlaying)
                Clear();
        }

        void Clear()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (!SimWorld.TryGetEntityManager(out var em))
                return;

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
        }

        bool Restore(EntityManager em, RunLaunch launch)
        {
            if (_save == null)
            {
                GameLog.Error("SimulationHost.Open failed — SaveService was not injected.");
                return false;
            }

            if (!_save.TryRead(launch.SaveId, out var snapshot))
            {
                GameLog.Error($"SimulationHost.Open failed — save '{launch.SaveId}' is missing.");
                return false;
            }

            var storyPack = defaultScenario != null ? defaultScenario.StoryPack : null;
            var workerSpeed = simRules != null ? simRules.WorkerSpeed : 2f;
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
                return false;

            PauseMenuScreen.Current?.RebuildViews();
            return true;
        }

        bool Seed(EntityManager em, RunLaunch launch)
        {
            var debug = launch.Debug;
            var scenario = debug ? debugScenario : defaultScenario;
            var storyPack = scenario != null ? scenario.StoryPack : null;
            if (storyPack == null && defaultScenario != null)
                storyPack = defaultScenario.StoryPack;

            if (debug && scenario == null)
                GameLog.Error("SimulationHost: DebugScenario is not assigned.");

            DifficultyProfile difficulty = null;
            if (scenario != null)
            {
                var requested = scenario.DefaultDifficulty;
                if (!scenario.TryResolveDifficulty(requested, out difficulty))
                {
                    GameLog.Error(
                        $"SimulationHost: difficulty {requested.name} is not allowed by scenario {scenario.name}.");
                    return false;
                }
            }

            GameLog.Info(
                $"Run kit: {(debug ? "Debug" : "Normal")} " +
                $"scenario={(scenario != null ? scenario.name : "null")} " +
                $"difficulty={(difficulty != null ? difficulty.name : "stamp defaults")}.");

            var workerSpeed = simRules != null ? simRules.WorkerSpeed : 2f;
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
                GameLog.Error("SimulationHost.Open failed — bootstrap did not create a session.");
                return false;
            }

            if (!ScenarioSetup.Apply(em, seeded, scenario, techCatalogs, workerSpeed, catalog, recipes, costs, stock, info))
            {
                GameLog.Error("SimulationHost.Open failed — scenario seed failed.");
                return false;
            }

            return true;
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
