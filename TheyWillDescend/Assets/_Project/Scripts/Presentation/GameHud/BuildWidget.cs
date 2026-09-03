using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Research;
using TheyWillDescend.Simulation.Session;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Build catalog from the building prototype buffer.
    /// Esc closes this overlay before Playing toggles player pause.
    /// </summary>
    public sealed class BuildWidget : MonoBehaviour
    {
        [SerializeField] Button buildModeButton;
        [SerializeField] GameObject buildCatalogPanel;
        [SerializeField, FormerlySerializedAs("selectCube3x6Button")] Button catalogButtonTemplate;
        [SerializeField, FormerlySerializedAs("selectCube2x2Button")] Button unusedLegacyCatalogButton;
        [SerializeField] BuildPlacementController placement;

        readonly List<Button> _spawnedButtons = new(8);

        bool _catalogOpen;
        bool _placedBound;
        Transform _buttonRoot;

        public static BuildWidget Current { get; private set; }

        public bool IsBusy => _catalogOpen || (placement != null && placement.IsPlacing);

        void Awake()
        {
            Current = this;
            HudButtons.Bind(buildModeButton, OnBuildModeClicked);
            HideLegacyButtons();
            SetCatalogVisible(false);
            BindPlacement();
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
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
            ResearchWidget.Current?.CloseIfBusy();
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
            SimCommands.TryPost(SimClockCommand.BuildLocked(true));
            GameLog.Info("BuildCatalog open (build locked).");
        }

        void Close(bool resumeSim)
        {
            _catalogOpen = false;
            SetCatalogVisible(false);
            placement?.CancelPlacing();

            if (resumeSim)
                SimCommands.TryPost(SimClockCommand.BuildLocked(false));
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

            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasBuffer<BuildingPrototype>(bag))
            {
                GameLog.Warning("Build catalog empty — SubScene / SimControl buildings not ready.");
                return;
            }

            var catalog = em.GetBuffer<BuildingPrototype>(bag);
            var names = em.HasBuffer<ResourceInfo>(bag) ? em.GetBuffer<ResourceInfo>(bag) : default;
            var viewCatalog = placement != null ? placement.Catalog : null;
            var count = 0;
            for (var i = 0; i < catalog.Length; i++)
            {
                var prototype = catalog[i];
                if (prototype.TypeId.IsEmpty)
                    continue;
                if (prototype.RequiresUnlock != 0
                    && !ResearchRules.IsBuildingUnlocked(em, prototype.TypeId))
                    continue;
                count++;
                var go = Instantiate(catalogButtonTemplate.gameObject, _buttonRoot, false);
                var typeId = prototype.TypeId.ToString();
                go.name = $"Catalog_{typeId}";
                go.SetActive(true);
                var button = go.GetComponent<Button>();
                var costs = em.HasBuffer<BuildingCatalogCost>(bag)
                    ? em.GetBuffer<BuildingCatalogCost>(bag)
                    : default;
                var cost = FormatBuildingCost(costs, prototype.TypeId, names);
                var prefab = viewCatalog != null ? viewCatalog.FindPrefab(typeId) : null;
                var title = BuildingView.NameOf(prefab);
                if (string.IsNullOrEmpty(title))
                    title = typeId;
                SetButtonLabel(button, string.IsNullOrEmpty(cost) ? title : $"{title}\n{cost}");
                HudButtons.Bind(button, () => BeginPlace(typeId));
                _spawnedButtons.Add(button);
            }

            if (count == 0)
                GameLog.Warning("Build catalog empty — SubScene / SimControl buildings not ready.");
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
            placement.Finished += OnPlacementFinished;
            _placedBound = true;
        }

        void UnbindPlacement()
        {
            if (!_placedBound || placement == null)
                return;
            placement.Finished -= OnPlacementFinished;
            _placedBound = false;
        }

        void OnPlacementFinished()
        {
            Close(resumeSim: true);
        }

        void SetCatalogVisible(bool visible)
        {
            if (buildCatalogPanel != null)
                buildCatalogPanel.SetActive(visible);
        }

        static string FormatBuildingCost(
            DynamicBuffer<BuildingCatalogCost> costs,
            in FixedString64Bytes typeId,
            DynamicBuffer<ResourceInfo> names)
        {
            if (!costs.IsCreated || typeId.IsEmpty)
                return string.Empty;

            var parts = new List<string>(4);
            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i];
                if (cost.TypeId != typeId || cost.Amount <= 0.0001f)
                    continue;
                parts.Add($"{(int)math.ceil(cost.Amount)} {ResourceDisplayName(names, cost.ResourceId)}");
            }

            return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
        }

        static string ResourceDisplayName(DynamicBuffer<ResourceInfo> names, in FixedString64Bytes resourceId)
        {
            if (names.IsCreated)
            {
                for (var i = 0; i < names.Length; i++)
                {
                    if (names[i].ResourceId == resourceId)
                        return names[i].DisplayName.ToString();
                }
            }

            return resourceId.ToString();
        }
    }
}
