using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Research;
using TheyWillDescend.Simulation.Session;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Build catalog on the <c>BuildCatalog</c> prefab: mode button, panel, and an
    /// authored row. One <see cref="catalogEntryPrefab"/> instance per unlocked
    /// building. The row, button, and erase control are not created in code.
    /// Esc closes this overlay before Playing toggles player pause.
    /// </summary>
    public sealed class BuildWidget : MonoBehaviour
    {
        [SerializeField] Button buildModeButton;
        [SerializeField] GameObject buildCatalogPanel;
        [SerializeField] Button catalogEntryPrefab;
        [SerializeField] Button eraseRoadButton;
        [SerializeField] BuildPlacementController placement;
        [SerializeField] RoadPaintController roadPaint;
        [SerializeField] RectTransform catalogEntriesRoot;

        readonly List<Button> _spawnedButtons = new(8);

        bool _catalogOpen;
        bool _placedBound;
        bool _roadBound;

        public static BuildWidget Current { get; private set; }

        public bool IsBusy =>
            _catalogOpen
            || (placement != null && placement.IsPlacing)
            || (roadPaint != null && roadPaint.IsPainting);

        void Awake()
        {
            Current = this;
            HudButtons.Bind(buildModeButton, OnBuildModeClicked);
            HudButtons.Bind(eraseRoadButton, BeginEraseRoads);
            SetCatalogVisible(false);
            BindPlacement();
            BindRoadPaint();
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
            HudButtons.Unbind(buildModeButton, OnBuildModeClicked);
            HudButtons.Unbind(eraseRoadButton, BeginEraseRoads);
            ClearSpawnedButtons();

            if (IsBusy)
                Close(resumeSim: true);

            UnbindPlacement();
            UnbindRoadPaint();
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
            BindRoadPaint();
            _catalogOpen = false;
            SetCatalogVisible(false);

            if (IsStroke(typeId))
            {
                if (roadPaint == null)
                {
                    GameLog.Error("BuildWidget: RoadPaintController is not assigned.");
                    Close(resumeSim: true);
                    return;
                }

                placement?.CancelPlacing();
                roadPaint.BeginPainting();
                if (!roadPaint.IsPainting)
                    Close(resumeSim: true);
                return;
            }

            if (placement == null)
                return;
            roadPaint?.CancelPainting();
            placement.BeginPlacing(typeId);
            if (!placement.IsPlacing)
                Close(resumeSim: true);
        }

        void BeginEraseRoads()
        {
            EnsurePlacement();
            BindRoadPaint();
            _catalogOpen = false;
            SetCatalogVisible(false);
            if (roadPaint == null)
            {
                GameLog.Error("BuildWidget: RoadPaintController is not assigned.");
                Close(resumeSim: true);
                return;
            }

            placement?.CancelPlacing();
            roadPaint.BeginErasing();
            if (!roadPaint.IsPainting)
                Close(resumeSim: true);
        }

        void OpenCatalog()
        {
            EnsurePlacement();
            placement?.CancelPlacing();
            roadPaint?.CancelPainting();
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
            roadPaint?.CancelPainting();

            if (resumeSim)
                SimCommands.TryPost(SimClockCommand.BuildLocked(false));
        }

        void RebuildCatalogButtons()
        {
            ClearSpawnedButtons();
            if (catalogEntriesRoot == null || catalogEntryPrefab == null)
            {
                GameLog.Error("BuildWidget: catalog entry prefab or entries root is not assigned.");
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
                var typeId = prototype.TypeId.ToString();
                var button = Instantiate(catalogEntryPrefab, catalogEntriesRoot);
                button.name = $"Catalog_{typeId}";
                button.gameObject.SetActive(true);
                var costs = em.HasBuffer<BuildingCatalogCost>(bag)
                    ? em.GetBuffer<BuildingCatalogCost>(bag)
                    : default;
                var cost = FormatBuildingCost(costs, prototype.TypeId, names);
                if (prototype.StrokePaint != 0 && !string.IsNullOrEmpty(cost))
                    cost += " / секция";
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

            if (eraseRoadButton != null && eraseRoadButton.transform.parent == catalogEntriesRoot)
                eraseRoadButton.transform.SetAsLastSibling();
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

        void BindRoadPaint()
        {
            if (_roadBound || roadPaint == null)
                return;
            roadPaint.Finished += OnPlacementFinished;
            _roadBound = true;
        }

        void UnbindPlacement()
        {
            if (!_placedBound || placement == null)
                return;
            placement.Finished -= OnPlacementFinished;
            _placedBound = false;
        }

        void UnbindRoadPaint()
        {
            if (!_roadBound || roadPaint == null)
                return;
            roadPaint.Finished -= OnPlacementFinished;
            _roadBound = false;
        }

        static bool IsStroke(string typeId)
        {
            if (typeId == RoadNetwork.TypeId)
                return true;
            if (!ContentId.TryEncode(typeId, out var key)
                || !SimWorld.TryGet(out var em, out var bag)
                || !em.HasBuffer<BuildingPrototype>(bag))
                return false;
            return BuildingCatalog.TryResolve(em.GetBuffer<BuildingPrototype>(bag), key, out var spec)
                && spec.StrokePaint != 0;
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
