using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Shell;
using _Project.Scripts.Simulation.Session;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.Presentation.GameHud
{
    /// <summary>
    /// Game-scene overlay HUD. Not shell menu — lives with the session scene.
    /// Build catalog is Presentation UI; sim freeze goes through <see cref="SimGate"/>.
    /// </summary>
    public sealed class GameHudBinder : MonoBehaviour, IGameplayEscapeHandler
    {
        [SerializeField] Button spawnAgentButton;
        [SerializeField] Button buildModeButton;
        [SerializeField] Agents.AgentSpawner agentSpawner;

        [SerializeField] GameObject buildCatalogPanel;
        [SerializeField] Button selectCubeButton;

        bool _catalogOpen;
        string _selectedBuildingId;

        public bool IsCatalogOpen => _catalogOpen;
        public string SelectedBuildingId => _selectedBuildingId;

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

            if (selectCubeButton != null)
                selectCubeButton.onClick.AddListener(OnSelectCubeClicked);

            SetCatalogVisible(false);
        }

        void OnDestroy()
        {
            if (spawnAgentButton != null)
                spawnAgentButton.onClick.RemoveListener(OnSpawnClicked);

            if (buildModeButton != null)
                buildModeButton.onClick.RemoveListener(OnBuildModeClicked);

            if (selectCubeButton != null)
                selectCubeButton.onClick.RemoveListener(OnSelectCubeClicked);

            if (_catalogOpen)
                CloseCatalog(resumeSim: true);
        }

        /// <summary>
        /// Shell calls this when Esc is pressed during Playing.
        /// Returns true if the catalog consumed Esc (do not open pause).
        /// </summary>
        public bool TryHandleEscape()
        {
            if (!_catalogOpen)
                return false;

            CloseCatalog(resumeSim: true);
            return true;
        }

        void OnSpawnClicked()
        {
            if (agentSpawner == null)
                agentSpawner = FindFirstObjectByType<Agents.AgentSpawner>();

            agentSpawner?.SpawnRandom();
        }

        void OnBuildModeClicked()
        {
            if (_catalogOpen)
                CloseCatalog(resumeSim: true);
            else
                OpenCatalog();
        }

        void OnSelectCubeClicked()
        {
            _selectedBuildingId = "StubCube";
            GameLog.Info(LogChannel.Presentation, "BuildCatalog selected: StubCube (ghost/place next).");
        }

        void OpenCatalog()
        {
            _catalogOpen = true;
            _selectedBuildingId = null;
            SetCatalogVisible(true);

            var gate = SimGate.Active;
            if (gate == null)
            {
                GameLog.Warning(LogChannel.Presentation, "BuildCatalog: SimGate.Active is null.");
                return;
            }

            // Catalog open ⇒ world time frozen. App stays in Playing (not PausedState).
            gate.Set(SimRunMode.Frozen);
            GameLog.Info(LogChannel.Presentation, "BuildCatalog → open (sim Frozen). Esc closes.");
        }

        void CloseCatalog(bool resumeSim)
        {
            if (!_catalogOpen)
                return;

            _catalogOpen = false;
            _selectedBuildingId = null;
            SetCatalogVisible(false);

            if (!resumeSim)
                return;

            var gate = SimGate.Active;
            if (gate == null)
            {
                GameLog.Warning(LogChannel.Presentation, "BuildCatalog: SimGate.Active is null on close.");
                return;
            }

            gate.Set(SimRunMode.Running);
            GameLog.Info(LogChannel.Presentation, "BuildCatalog → closed (sim Running).");
        }

        void SetCatalogVisible(bool visible)
        {
            if (buildCatalogPanel != null)
                buildCatalogPanel.SetActive(visible);
        }
    }
}
