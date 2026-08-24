using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Shell;
using TheyWillDescend.Simulation.Io;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Build catalog from the session building buffer via <see cref="SimIo"/>.
    /// Esc closes this overlay before Playing toggles player pause.
    /// </summary>
    public sealed class BuildWidget : MonoBehaviour, IGameplayEscapeHandler
    {
        [SerializeField] Button buildModeButton;
        [SerializeField] GameObject buildCatalogPanel;
        [SerializeField, FormerlySerializedAs("selectCube3x6Button")] Button catalogButtonTemplate;
        [SerializeField, FormerlySerializedAs("selectCube2x2Button")] Button unusedLegacyCatalogButton;
        [SerializeField] BuildPlacementController placement;

        readonly List<BuildingCatalogEntry> _catalog = new(8);
        readonly List<Button> _spawnedButtons = new(8);

        bool _catalogOpen;
        bool _placedBound;
        Transform _buttonRoot;

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
            HideLegacyButtons();
            SetCatalogVisible(false);
            BindPlacement();
        }

        void OnDestroy()
        {
            HudButtons.Unbind(buildModeButton, OnBuildModeClicked);
            ClearSpawnedButtons();

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

        void OnBuildModeClicked()
        {
            if (IsBusy)
                Close(resumeSim: true);
            else
                OpenCatalog();
        }

        void BeginPlace(string typeId)
        {
            EnsurePlacement();
            if (placement == null)
                return;

            _catalogOpen = false;
            SetCatalogVisible(false);
            placement.BeginPlacing(typeId);
            if (!placement.IsPlacing)
                Close(resumeSim: true);
        }

        void OpenCatalog()
        {
            EnsurePlacement();
            placement?.CancelPlacing();
            RebuildCatalogButtons();

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

        void RebuildCatalogButtons()
        {
            HideLegacyButtons();
            ClearSpawnedButtons();
            EnsureButtonRoot();
            if (_buttonRoot == null || catalogButtonTemplate == null)
            {
                GameLog.Error("BuildWidget: catalog button template missing.");
                return;
            }

            SimIo.CopyBuildingCatalog(_catalog);
            if (_catalog.Count == 0)
            {
                GameLog.Warning("Build catalog empty — SubScene / SimControl buildings not ready.");
                return;
            }

            for (var i = 0; i < _catalog.Count; i++)
            {
                var entry = _catalog[i];
                var go = Instantiate(catalogButtonTemplate.gameObject, _buttonRoot, false);
                go.name = $"Catalog_{entry.TypeId}";
                go.SetActive(true);
                var button = go.GetComponent<Button>();
                var typeId = entry.TypeId;
                var cost = SimIo.FormatBuildingCost(typeId);
                var title = string.IsNullOrEmpty(entry.DisplayName)
                    ? $"{entry.WidthClusters}×{entry.DepthRadialRings}"
                    : entry.DisplayName;
                SetButtonLabel(button, string.IsNullOrEmpty(cost) ? title : $"{title}\n{cost}");
                HudButtons.Bind(button, () => BeginPlace(typeId));
                _spawnedButtons.Add(button);
            }
        }

        void EnsureButtonRoot()
        {
            if (_buttonRoot != null || buildCatalogPanel == null || catalogButtonTemplate == null)
                return;

            var root = new GameObject("CatalogEntries", typeof(RectTransform));
            root.transform.SetParent(buildCatalogPanel.transform, false);
            var rt = root.GetComponent<RectTransform>();
            var templateRt = catalogButtonTemplate.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = templateRt != null
                ? templateRt.anchoredPosition
                : new Vector2(140f, 120f);
            rt.sizeDelta = new Vector2(templateRt != null ? templateRt.sizeDelta.x : 240f, 0f);

            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 12f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            var fitter = root.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _buttonRoot = root.transform;
        }

        void HideLegacyButtons()
        {
            if (catalogButtonTemplate != null)
                catalogButtonTemplate.gameObject.SetActive(false);
            if (unusedLegacyCatalogButton != null)
                unusedLegacyCatalogButton.gameObject.SetActive(false);
        }

        void ClearSpawnedButtons()
        {
            for (var i = 0; i < _spawnedButtons.Count; i++)
            {
                var button = _spawnedButtons[i];
                if (button == null)
                    continue;
                Destroy(button.gameObject);
            }

            _spawnedButtons.Clear();
        }

        static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
                return;
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
                tmp.text = label;
        }

        void EnsurePlacement()
        {
            if (placement == null)
            {
                GameLog.Error("BuildWidget: BuildPlacementController is not assigned.");
                return;
            }

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
