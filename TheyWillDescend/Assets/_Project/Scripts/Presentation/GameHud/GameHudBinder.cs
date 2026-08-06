using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Presentation.City;
using _Project.Scripts.Shell;
using _Project.Scripts.Simulation.City;
using _Project.Scripts.Simulation.Session;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.Presentation.GameHud
{
    /// <summary>
    /// Game-scene overlay HUD. Catalog + entry into footprint placing.
    /// </summary>
    public sealed class GameHudBinder : MonoBehaviour, IGameplayEscapeHandler
    {
        [SerializeField] Button spawnAgentButton;
        [SerializeField] Button buildModeButton;
        [SerializeField] Agents.AgentSpawner agentSpawner;

        [SerializeField] GameObject buildCatalogPanel;
        [SerializeField] Button selectCube3x6Button;
        [SerializeField] Button selectCube2x2Button;
        [SerializeField] BuildPlacementController placement;

        bool _catalogOpen;

        public bool IsCatalogOpen => _catalogOpen;

        void OnEnable()
        {
            GameplayEscapeRouter.Active = this;
        }

        void OnDisable()
        {
            if (GameplayEscapeRouter.Active == this)
                GameplayEscapeRouter.Active = null;
        }

        void Awake()
        {
            if (spawnAgentButton != null)
                spawnAgentButton.onClick.AddListener(OnSpawnClicked);

            if (buildModeButton != null)
                buildModeButton.onClick.AddListener(OnBuildModeClicked);

            if (selectCube3x6Button != null)
                selectCube3x6Button.onClick.AddListener(OnSelectCube3x6);

            if (selectCube2x2Button != null)
                selectCube2x2Button.onClick.AddListener(OnSelectCube2x2);

            SetCatalogVisible(false);
        }

        void OnDestroy()
        {
            if (spawnAgentButton != null)
                spawnAgentButton.onClick.RemoveListener(OnSpawnClicked);

            if (buildModeButton != null)
                buildModeButton.onClick.RemoveListener(OnBuildModeClicked);

            if (selectCube3x6Button != null)
                selectCube3x6Button.onClick.RemoveListener(OnSelectCube3x6);

            if (selectCube2x2Button != null)
                selectCube2x2Button.onClick.RemoveListener(OnSelectCube2x2);

            if (_catalogOpen || (placement != null && placement.IsPlacing))
                ExitBuildUi(resumeSim: true);
        }

        public bool TryHandleEscape()
        {
            if (placement != null && placement.IsPlacing)
            {
                ExitBuildUi(resumeSim: true);
                return true;
            }

            if (_catalogOpen)
            {
                ExitBuildUi(resumeSim: true);
                return true;
            }

            return false;
        }

        void OnSpawnClicked()
        {
            if (agentSpawner == null)
                agentSpawner = FindFirstObjectByType<Agents.AgentSpawner>();

            agentSpawner?.SpawnRandom();
        }

        void OnBuildModeClicked()
        {
            if (_catalogOpen || (placement != null && placement.IsPlacing))
                ExitBuildUi(resumeSim: true);
            else
                OpenCatalog();
        }

        void OnSelectCube3x6() => BeginPlace(BuildingFootprint.House6x2, "House 6x2");

        void OnSelectCube2x2() => BeginPlace(BuildingFootprint.Cube2x2, "Cube 2x2");

        void BeginPlace(BuildingFootprint footprint, string label)
        {
            EnsurePlacement();
            placement.SetFootprint(footprint);

            _catalogOpen = false;
            SetCatalogVisible(false);
            placement.BeginPlacing();

            GameLog.Info(
                LogChannel.Presentation,
                $"Selected {label}. Click to place, Esc cancels.");
        }

        void OpenCatalog()
        {
            EnsurePlacement();
            placement.CancelPlacing();

            _catalogOpen = true;
            SetCatalogVisible(true);
            SetSimFrozen(true);
            GameLog.Info(LogChannel.Presentation, "BuildCatalog → open (sim Frozen).");
        }

        void ExitBuildUi(bool resumeSim)
        {
            _catalogOpen = false;
            SetCatalogVisible(false);
            placement?.CancelPlacing();

            if (resumeSim)
                SetSimFrozen(false);
        }

        void SetSimFrozen(bool frozen)
        {
            var gate = SimGate.Active;
            if (gate == null)
            {
                GameLog.Warning(LogChannel.Presentation, "Build UI: SimGate.Active is null.");
                return;
            }

            gate.Set(frozen ? SimRunMode.Frozen : SimRunMode.Running);
            GameLog.Info(
                LogChannel.Presentation,
                frozen ? "Build UI → sim Frozen." : "Build UI → sim Running.");
        }

        void EnsurePlacement()
        {
            if (placement == null)
                placement = FindFirstObjectByType<BuildPlacementController>();
        }

        void SetCatalogVisible(bool visible)
        {
            if (buildCatalogPanel != null)
                buildCatalogPanel.SetActive(visible);
        }
    }
}
