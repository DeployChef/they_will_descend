using TheyWillDescend.Presentation.City;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Right-dock building card. Pulls workplace from the building entity; sends assign commands.
    /// </summary>
    public sealed class BuildingInspectPanel : MonoBehaviour
    {
        [SerializeField] BuildingSelection selection;
        public BuildingSelection Selection => selection;
        [SerializeField] GameObject card;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text workers;
        [SerializeField] TMP_Text idle;
        [SerializeField] TMP_Text status;
        [SerializeField] Button minusButton;
        [SerializeField] Button plusButton;
        [SerializeField] Button maxMinusButton;
        [SerializeField] Button maxPlusButton;
        [SerializeField] Button powerButton;
        [SerializeField] Button closeButton;
        [SerializeField] Image workFill;

        void Awake()
        {
            EnsureExtraButtons();
            HudButtons.Bind(minusButton, OnMinus);
            HudButtons.Bind(plusButton, OnPlus);
            HudButtons.Bind(maxMinusButton, OnMaxMinus);
            HudButtons.Bind(maxPlusButton, OnMaxPlus);
            HudButtons.Bind(powerButton, OnPower);
            HudButtons.Bind(closeButton, OnClose);
            if (Application.isPlaying)
                Hide();
        }

        void OnDestroy()
        {
            HudButtons.Unbind(minusButton, OnMinus);
            HudButtons.Unbind(plusButton, OnPlus);
            HudButtons.Unbind(maxMinusButton, OnMaxMinus);
            HudButtons.Unbind(maxPlusButton, OnMaxPlus);
            HudButtons.Unbind(powerButton, OnPower);
            HudButtons.Unbind(closeButton, OnClose);
        }

        void EnsureExtraButtons()
        {
            if (maxMinusButton == null && minusButton != null)
                maxMinusButton = CloneButton(minusButton, "MaxMinusButton", "Max");
            else
                HudButtons.SetLabel(maxMinusButton, "Max");

            if (maxPlusButton == null && plusButton != null)
                maxPlusButton = CloneButton(plusButton, "MaxPlusButton", "Max");
            else
                HudButtons.SetLabel(maxPlusButton, "Max");

            if (powerButton == null && plusButton != null)
                powerButton = CloneButton(plusButton, "PowerButton", "Stop");

            LayoutStaffButtons();
        }

        static Button CloneButton(Button source, string name, string label)
        {
            var go = Object.Instantiate(source.gameObject, source.transform.parent);
            go.name = name;
            var button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            HudButtons.SetLabel(button, label);
            return button;
        }

        void LayoutStaffButtons()
        {
            const float edge = 16f;
            const float gap = 6f;
            const float maxWidth = 56f;
            var rowY = minusButton != null
                ? minusButton.GetComponent<RectTransform>().anchoredPosition.y
                : -232f;
            var rowH = minusButton != null
                ? minusButton.GetComponent<RectTransform>().sizeDelta.y
                : 40f;

            PlaceOnLeft(maxMinusButton, edge, rowY, maxWidth, rowH);
            PlaceOnLeft(minusButton, edge + maxWidth + gap, rowY, 48f, rowH);
            PlaceOnRight(maxPlusButton, edge, rowY, maxWidth, rowH);
            PlaceOnRight(plusButton, edge + maxWidth + gap, rowY, 48f, rowH);
            PlacePowerRow(powerButton, rowY, rowH);

            if (workers != null)
            {
                var rt = workers.rectTransform;
                rt.sizeDelta = new Vector2(-268f, rt.sizeDelta.y);
            }

            var powerRt = powerButton != null ? powerButton.GetComponent<RectTransform>() : null;
            var idleRt = idle != null ? idle.rectTransform : null;
            var statusRt = status != null ? status.rectTransform : null;
            var barRt = workFill != null && workFill.transform.parent != null
                ? workFill.transform.parent as RectTransform
                : null;
            PushBelow(idleRt, powerRt, 8f);
            PushBelow(statusRt, idleRt, 8f);
            PushBelow(barRt, statusRt, 12f);
        }

        static void PlaceOnLeft(Button button, float x, float y, float width, float height)
        {
            if (button == null)
                return;

            var rt = button.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        static void PlaceOnRight(Button button, float xFromRight, float y, float width, float height)
        {
            if (button == null)
                return;

            var rt = button.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-xFromRight, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        static void PlacePowerRow(Button power, float rowY, float rowH)
        {
            if (power == null)
                return;

            var rt = power.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, rowY - rowH - 8f);
            rt.sizeDelta = new Vector2(-32f, 40f);
        }

        static void PushBelow(RectTransform target, RectTransform above, float gap)
        {
            if (target == null || above == null)
                return;

            var needed = above.anchoredPosition.y - above.sizeDelta.y - gap;
            if (target.anchoredPosition.y > needed)
                target.anchoredPosition = new Vector2(target.anchoredPosition.x, needed);
        }

        void Update()
        {
            if (selection == null)
                return;

            var id = selection.SelectedBuildingId;
            if (id <= 0)
            {
                Hide();
                return;
            }

            Show(id);
        }

        void Hide()
        {
            if (card != null)
                card.SetActive(false);
        }

        void Show(int id)
        {
            if (!SimWorld.TryGet(out var em, out var bag) || !TryFindBuilding(em, id, out var entity, out var building))
            {
                Hide();
                return;
            }

            if (em.HasComponent<Headquarters>(entity))
            {
                Hide();
                return;
            }

            var constructing = em.HasComponent<Construction>(entity);
            var workplace = em.HasComponent<Workplace>(entity)
                ? em.GetComponentData<Workplace>(entity)
                : default;
            var displayName = building.TypeId.ToString();
            var slots = em.HasComponent<BuildingType>(entity)
                ? em.GetComponentData<BuildingType>(entity).WorkplaceSlots
                : 0;
            var viewCatalog = FindViewCatalog();
            if (viewCatalog != null)
            {
                var prefab = viewCatalog.FindPrefab(building.TypeId.ToString());
                var viewName = BuildingView.NameOf(prefab);
                if (!string.IsNullOrEmpty(viewName))
                    displayName = viewName;
            }

            if (card != null)
                card.SetActive(true);
            if (title != null)
                title.text = string.IsNullOrEmpty(displayName) ? $"Building {building.TypeId}" : displayName;

            var occupied = workplace.AssignedCount;
            var working = workplace.WorkingCount;
            var paused = workplace.IsPaused;
            var onShift = TryGetGameTime(em, out var time) && time.IsWorkShift;
            if (slots < 0)
                slots = 0;
            var idleCount = CountIdleWorkers(em);
            if (workers != null)
                workers.text = $"{occupied} / {slots}";
            if (idle != null)
                idle.text = $"Idle workers  {idleCount}";

            if (constructing)
            {
                var construction = em.GetComponentData<Construction>(entity);
                if (subtitle != null)
                    subtitle.text = "Under construction";
                if (status != null)
                    status.text = "Crew locked until the house stands.";
                if (workFill != null)
                    workFill.fillAmount = construction.Normalized;
            }
            else if (!onShift)
            {
                if (subtitle != null)
                    subtitle.text = FormatRecipeSubtitle(em, entity, bag, slots, 0f);
                if (status != null)
                {
                    if (occupied == 0)
                        status.text =
                            $"Off shift ({time.WorkShiftStartHour:00}:00–{time.WorkShiftEndHour:00}:00). No one assigned.";
                    else
                        status.text = paused
                            ? "Off shift. Crew at the pyramid. Production is stopped."
                            : "Off shift. Crew walks around the pyramid.";
                }

                if (workFill != null)
                    workFill.fillAmount = Workplace.Load01(occupied, slots);
            }
            else if (paused)
            {
                if (subtitle != null)
                    subtitle.text = FormatRecipeSubtitle(em, entity, bag, slots, 0f);
                if (status != null)
                    status.text = occupied == 0
                        ? "Production stopped. No one assigned."
                        : "Production stopped. Crew stays assigned.";
                if (workFill != null)
                    workFill.fillAmount = Workplace.Load01(occupied, slots);
            }
            else
            {
                var productionLoad = Workplace.Load01(working, slots);
                if (subtitle != null)
                    subtitle.text = FormatRecipeSubtitle(em, entity, bag, slots, productionLoad);
                if (status != null)
                {
                    if (occupied == 0)
                        status.text = "No one assigned.";
                    else if (working >= occupied)
                        status.text = "All workers on site.";
                    else
                        status.text = $"{working} on site, {occupied - working} walking.";
                }

                if (workFill != null)
                    workFill.fillAmount = Workplace.Load01(occupied, slots);
            }

            var canStaff = !constructing && slots > 0;
            HudButtons.SetInteractable(plusButton, canStaff && occupied < slots && idleCount > 0);
            HudButtons.SetInteractable(minusButton, canStaff && occupied > 0);
            HudButtons.SetInteractable(maxPlusButton, canStaff && occupied < slots && idleCount > 0);
            HudButtons.SetInteractable(maxMinusButton, canStaff && occupied > 0);
            HudButtons.SetInteractable(powerButton, !constructing && slots > 0);
            HudButtons.SetLabel(powerButton, paused ? "Start" : "Stop");
            HudButtons.Tint(powerButton, !paused);
        }

        static bool TryFindBuilding(EntityManager em, int id, out Entity entity, out Building building)
        {
            entity = Entity.Null;
            building = default;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            for (var i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].Id != id)
                    continue;
                entity = entities[i];
                building = buildings[i];
                return true;
            }

            return false;
        }

        static bool TryGetGameTime(EntityManager em, out GameTime time)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameTime>());
            if (query.IsEmptyIgnoreFilter)
            {
                time = default;
                return false;
            }

            time = query.GetSingleton<GameTime>();
            return true;
        }

        static TheyWillDescend.Simulation.Content.BuildingCatalogAsset FindViewCatalog()
        {
            var placement = Object.FindFirstObjectByType<BuildPlacementController>();
            return placement != null ? placement.Catalog : null;
        }

        static string FormatRecipeSubtitle(
            EntityManager em,
            Entity buildingEntity,
            Entity session,
            int slots,
            float productionLoad)
        {
            var role = slots > 0 ? "Workplace" : "Building";
            if (!em.HasBuffer<BuildingRecipeLine>(buildingEntity))
                return role;

            var recipes = em.GetBuffer<BuildingRecipeLine>(buildingEntity);
            var hasNames = em.HasBuffer<ResourceInfo>(session);
            var names = hasNames ? em.GetBuffer<ResourceInfo>(session) : default;
            var parts = new System.Text.StringBuilder(role);
            var any = false;
            for (var i = 0; i < recipes.Length; i++)
            {
                var line = recipes[i];
                if (line.PerHour <= 0.0001f)
                    continue;
                if (!any)
                {
                    parts.Append("  ·  ");
                    any = true;
                }
                else
                    parts.Append("  ");

                var name = hasNames
                    ? DisplayName(names, line.ResourceId)
                    : line.ResourceId.ToString();
                var sign = line.Kind == BuildingRecipeKind.Input ? "−" : "+";
                var current = line.PerHour * productionLoad;
                parts.Append(name);
                parts.Append(' ');
                parts.Append(sign);
                parts.Append(current.ToString("0.##"));
                parts.Append("/h");
            }

            return parts.ToString();
        }

        static string DisplayName(DynamicBuffer<ResourceInfo> names, FixedString64Bytes resourceId)
        {
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i].ResourceId != resourceId)
                    continue;
                var display = names[i].DisplayName.ToString();
                return string.IsNullOrEmpty(display) ? resourceId.ToString() : display;
            }

            return resourceId.ToString();
        }

        static int CountIdleWorkers(EntityManager em)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<AgentId>(),
                ComponentType.ReadOnly<AgentAssignment>());
            using var assignments = query.ToComponentDataArray<AgentAssignment>(Allocator.Temp);
            var idle = 0;
            for (var i = 0; i < assignments.Length; i++)
            {
                if (assignments[i].WorkplaceBuildingId == 0)
                    idle++;
            }

            return idle;
        }

        void OnMinus()
        {
            if (selection != null && selection.SelectedBuildingId > 0)
                SimCommands.TryPost(new UnassignWorkerCommand { BuildingId = selection.SelectedBuildingId });
        }

        void OnPlus()
        {
            if (selection != null && selection.SelectedBuildingId > 0)
                SimCommands.TryPost(new AssignWorkerCommand
                {
                    BuildingId = selection.SelectedBuildingId
                });
        }

        void OnMaxMinus()
        {
            if (selection == null || selection.SelectedBuildingId <= 0)
                return;
            SimCommands.TryPost(new UnassignWorkerCommand
            {
                BuildingId = selection.SelectedBuildingId,
                Count = 256
            });
            SimCommands.Playback();
        }

        void OnMaxPlus()
        {
            if (selection == null || selection.SelectedBuildingId <= 0)
                return;
            SimCommands.TryPost(new AssignWorkerCommand
            {
                BuildingId = selection.SelectedBuildingId,
                Count = 256
            });
            SimCommands.Playback();
        }

        void OnPower()
        {
            if (selection == null || selection.SelectedBuildingId <= 0)
                return;
            if (!SimWorld.TryGet(out var em, out _) || !TryFindBuilding(em, selection.SelectedBuildingId, out var entity, out _))
                return;
            var paused = em.HasComponent<Workplace>(entity) && em.GetComponentData<Workplace>(entity).IsPaused;
            SimCommands.TryPost(new SetWorkplacePausedCommand
            {
                BuildingId = selection.SelectedBuildingId,
                Paused = paused ? (byte)0 : (byte)1
            });
            SimCommands.Playback();
        }

        void OnClose()
        {
            selection?.Deselect();
            Hide();
        }
    }
}
