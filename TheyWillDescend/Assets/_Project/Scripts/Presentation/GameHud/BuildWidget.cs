using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Shell;
using TheyWillDescend.Simulation.City;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Catalog + ghost placement. Owns BuildLocked; Esc closes this overlay
    /// before Playing toggles player pause.
    /// </summary>
    public sealed class BuildWidget : MonoBehaviour, IGameplayEscapeHandler
    {
        [SerializeField] Button buildModeButton;
        [SerializeField] GameObject buildCatalogPanel;
        [SerializeField] Button selectCube3x6Button;
        [SerializeField] Button selectCube2x2Button;
        [SerializeField] BuildPlacementController placement;

        bool _catalogOpen;
        bool _placedBound;

        public bool IsBusy => _catalogOpen || (placement != null && placement.IsPlacing);

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
            HudButtons.Bind(buildModeButton, OnBuildModeClicked);
            HudButtons.Bind(selectCube3x6Button, OnSelectCube3x6);
            HudButtons.Bind(selectCube2x2Button, OnSelectCube2x2);
            SetCatalogVisible(false);
            BindPlacement();
        }

        void OnDestroy()
        {
            HudButtons.Unbind(buildModeButton, OnBuildModeClicked);
            HudButtons.Unbind(selectCube3x6Button, OnSelectCube3x6);
            HudButtons.Unbind(selectCube2x2Button, OnSelectCube2x2);

            if (IsBusy)
                Close(resumeSim: true);

            UnbindPlacement();
        }

        public bool TryHandleEscape()
        {
            if (!IsBusy)
                return false;
            Close(resumeSim: true);
            return true;
        }

        public void CloseIfBusy()
        {
            if (IsBusy)
                Close(resumeSim: true);
        }

        public void PumpViews()
        {
            EnsurePlacement();
            placement?.PumpViews();
        }

        public void RebuildViews()
        {
            EnsurePlacement();
            placement?.RebuildViews();
        }

        void OnBuildModeClicked()
        {
            if (IsBusy)
                Close(resumeSim: true);
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

        void Close(bool resumeSim)
        {
            _catalogOpen = false;
            SetCatalogVisible(false);
            placement?.CancelPlacing();

            if (resumeSim)
                SimGate.Active?.SetBuildLocked(false);
        }

        void EnsurePlacement()
        {
            if (placement == null)
                placement = FindFirstObjectByType<BuildPlacementController>();
            BindPlacement();
        }

        void BindPlacement()
        {
            if (_placedBound || placement == null)
                return;
            placement.Placed += OnBuildingPlaced;
            _placedBound = true;
        }

        void UnbindPlacement()
        {
            if (!_placedBound || placement == null)
                return;
            placement.Placed -= OnBuildingPlaced;
            _placedBound = false;
        }

        void OnBuildingPlaced()
        {
            Close(resumeSim: true);
        }

        void SetCatalogVisible(bool visible)
        {
            if (buildCatalogPanel != null)
                buildCatalogPanel.SetActive(visible);
        }
    }
}
