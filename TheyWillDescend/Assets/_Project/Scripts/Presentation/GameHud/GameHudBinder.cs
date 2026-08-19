using TheyWillDescend.App;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Shell;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Time;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Game HUD: time tool, catalog, spawn, one-slot save/load.
    /// Clock buttons talk only to <see cref="SimGate"/> — never Set(Running/Frozen).
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

        [SerializeField] Button pauseButton;
        [SerializeField] Button speed1Button;
        [SerializeField] Button speed2Button;
        [SerializeField] Button speed3Button;
        [SerializeField] TMP_Text clockLabel;
        [SerializeField] Button saveButton;
        [SerializeField] Button loadButton;

        bool _catalogOpen;
        EntityQuery _timeQuery;

        public bool IsCatalogOpen => _catalogOpen;

        void OnEnable()
        {
            GameplayEscapeRouter.Active = this;
        }

        void OnDisable()
        {
            if (GameplayEscapeRouter.Active == this)
                GameplayEscapeRouter.Active = null;
            DisposeTimeQuery();
        }

        void Awake()
        {
            Bind(spawnAgentButton, OnSpawnClicked);
            Bind(buildModeButton, OnBuildModeClicked);
            Bind(selectCube3x6Button, OnSelectCube3x6);
            Bind(selectCube2x2Button, OnSelectCube2x2);
            Bind(pauseButton, OnPauseClicked);
            Bind(speed1Button, () => OnSpeedClicked(1));
            Bind(speed2Button, () => OnSpeedClicked(2));
            Bind(speed3Button, () => OnSpeedClicked(3));
            Bind(saveButton, OnSaveClicked);
            Bind(loadButton, OnLoadClicked);
            SetCatalogVisible(false);
        }

        void OnDestroy()
        {
            Unbind(spawnAgentButton, OnSpawnClicked);
            Unbind(buildModeButton, OnBuildModeClicked);
            Unbind(selectCube3x6Button, OnSelectCube3x6);
            Unbind(selectCube2x2Button, OnSelectCube2x2);
            Unbind(pauseButton, OnPauseClicked);
            Unbind(saveButton, OnSaveClicked);
            Unbind(loadButton, OnLoadClicked);

            if (_catalogOpen || (placement != null && placement.IsPlacing))
                ExitBuildUi(resumeSim: true);

            DisposeTimeQuery();
        }

        void Update()
        {
            RefreshClockHud();
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

        void OnPauseClicked()
        {
            SimGate.Active?.TogglePlayerPause();
        }

        void OnSpeedClicked(int speed)
        {
            SimGate.Active?.SetSpeed(speed);
        }

        void OnSaveClicked()
        {
            if (_catalogOpen || (placement != null && placement.IsPlacing))
                ExitBuildUi(resumeSim: true);

            var snapshot = RunSessionSnapshot.Capture();
            RunSnapshotStore.Write(snapshot);
        }

        void OnLoadClicked()
        {
            if (!RunSnapshotStore.TryRead(out var snapshot))
                return;

            if (_catalogOpen || (placement != null && placement.IsPlacing))
                ExitBuildUi(resumeSim: true);

            EnsureSpawner();
            EnsurePlacement();
            RunSessionSnapshot.Apply(snapshot);
            agentSpawner?.PumpViews();
            placement?.PumpViews();
        }

        void OnSpawnClicked()
        {
            EnsureSpawner();
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

        void OnSelectCube2x2() => BeginPlace(BuildingFootprint.Cube2x2, "House 2x2");

        void BeginPlace(BuildingFootprint footprint, string label)
        {
            EnsurePlacement();
            placement.SetFootprint(footprint);

            _catalogOpen = false;
            SetCatalogVisible(false);
            placement.BeginPlacing();

            GameLog.Info($"Selected {label}. Click to place, Esc cancels.");
        }

        void OpenCatalog()
        {
            EnsurePlacement();
            placement.CancelPlacing();

            _catalogOpen = true;
            SetCatalogVisible(true);
            SimGate.Active?.SetBuildLocked(true);
            GameLog.Info("BuildCatalog open (build locked).");
        }

        void ExitBuildUi(bool resumeSim)
        {
            _catalogOpen = false;
            SetCatalogVisible(false);
            placement?.CancelPlacing();

            if (resumeSim)
                SimGate.Active?.SetBuildLocked(false);
        }

        void RefreshClockHud()
        {
            var gate = SimGate.Active;
            var buildLocked = gate != null && gate.BuildLocked;
            SetInteractable(speed1Button, !buildLocked);
            SetInteractable(speed2Button, !buildLocked);
            SetInteractable(speed3Button, !buildLocked);
            SetInteractable(pauseButton, !buildLocked);

            HighlightSpeed(gate != null ? gate.Speed : 1);
            HighlightPause(gate != null && gate.PlayerPaused);

            if (clockLabel == null)
                return;

            if (!TryGetGameTime(out var time))
            {
                clockLabel.text = "Day --  --:--";
                return;
            }

            clockLabel.text = GameClockFormat.Format(time);
        }

        World _timeWorld;

        bool TryGetGameTime(out GameTime time)
        {
            time = default;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            if (_timeQuery == default || _timeWorld != world)
            {
                DisposeTimeQuery();
                _timeQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GameTime>());
                _timeWorld = world;
            }

            if (_timeQuery.IsEmptyIgnoreFilter)
                return false;

            time = _timeQuery.GetSingleton<GameTime>();
            return true;
        }

        void DisposeTimeQuery()
        {
            if (_timeQuery == default)
                return;
            _timeQuery.Dispose();
            _timeQuery = default;
            _timeWorld = null;
        }

        void HighlightSpeed(int speed)
        {
            Tint(speed1Button, speed == 1);
            Tint(speed2Button, speed == 2);
            Tint(speed3Button, speed == 3);
        }

        void HighlightPause(bool paused)
        {
            Tint(pauseButton, paused);
        }

        static void Tint(Button button, bool on)
        {
            if (button == null)
                return;
            var colors = button.colors;
            colors.normalColor = on ? new Color(0.35f, 0.7f, 1f, 1f) : Color.white;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
        }

        static void SetInteractable(Button button, bool value)
        {
            if (button != null)
                button.interactable = value;
        }

        void EnsureSpawner()
        {
            if (agentSpawner == null)
                agentSpawner = FindFirstObjectByType<Agents.AgentSpawner>();
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

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.RemoveListener(action);
        }
    }
}
