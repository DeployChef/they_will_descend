using TheyWillDescend.Presentation.City;
using TheyWillDescend.Content;
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
        [SerializeField] TMP_Text workers;
        [SerializeField] TMP_Text idle;
        [SerializeField] TMP_Text status;
        [Tooltip("Тип: Workplace / Building")]
        [SerializeField] TMP_Text buildingRole;
        [Tooltip("Название производимого ресурса")]
        [SerializeField] TMP_Text resourceName;
        [Tooltip("Количество ресурса в час (без суффикса), например +2.5")]
        [SerializeField] TMP_Text resourceRate;
        [SerializeField] Button minusButton;
        [SerializeField] Button plusButton;
        [SerializeField] Button maxMinusButton;
        [SerializeField] Button maxPlusButton;
        [SerializeField] Button powerButton;
        [SerializeField] Button closeButton;
        [SerializeField] Button destroyButton;
        [SerializeField] Image workFill;
        [Tooltip("Спрайт для состояния Start (производство остановлено)")]
        [SerializeField] Sprite powerStartSprite;
        [Tooltip("Спрайт для состояния Stop (производство работает)")]
        [SerializeField] Sprite powerStopSprite;
        [Tooltip("Image, в который подставляется спрайт. Если не назначен — берётся Image с самой кнопки")]
        [SerializeField] Image powerIcon;

        BuildPlacementController _placement;

        void Awake()
        {
            EnsureExtraButtons();
            EnsureDestroyButton();
            HudButtons.Bind(minusButton, OnMinus);
            HudButtons.Bind(plusButton, OnPlus);
            HudButtons.Bind(maxMinusButton, OnMaxMinus);
            HudButtons.Bind(maxPlusButton, OnMaxPlus);
            HudButtons.Bind(powerButton, OnPower);
            HudButtons.Bind(closeButton, OnClose);
            HudButtons.Bind(destroyButton, OnDestroyBuilding);
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
            HudButtons.Unbind(destroyButton, OnDestroyBuilding);
        }

        void EnsureExtraButtons()
        {
            if (maxMinusButton == null && minusButton != null)
                maxMinusButton = CloneButton(minusButton, "MaxMinusButton", "Max");

            if (maxPlusButton == null && plusButton != null)
                maxPlusButton = CloneButton(plusButton, "MaxPlusButton", "Max");

            if (powerButton == null && plusButton != null)
                powerButton = CloneButton(plusButton, "PowerButton", "Stop");
        }

        void EnsureDestroyButton()
        {
            if (destroyButton != null)
                return;
            var root = card != null ? card.transform : transform;
            var marked = root.Find("ImageDestroyBuilding");
            if (marked == null)
                return;
            destroyButton = marked.GetComponent<Button>();
            if (destroyButton == null)
                destroyButton = marked.gameObject.AddComponent<Button>();
            var graphic = marked.GetComponent<Image>();
            if (graphic != null)
                destroyButton.targetGraphic = graphic;
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
            if (id == 1)
            {
                Hide();
                return;
            }

            if (!SimWorld.TryGet(out var em, out var bag) || !TryFindBuilding(em, id, out var entity, out var building))

            {
                selection?.ClearIf(id);
                Hide();
                return;
            }


            var constructing = em.HasComponent<Construction>(entity);
            var construction = constructing ? em.GetComponentData<Construction>(entity) : default;
            var dismantling = constructing && construction.IsDismantling;
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
            CountWorkforce(em, out var totalWorkers, out var totalDesired, out var freeWorkers);
            if (workers != null)
            {
                workers.text = $"{workplace.DesiredWorkers} / {slots}";
            }

            if (idle != null)
                idle.text = $"Idle workers  {freeWorkers}";


            if (constructing)
            {
                CountConstructionCrew(em, building.Id, out var crewAssigned, out var crewArrived);
                if (workers != null)
                    workers.text = $"{construction.Normalized * 100f:0}%";
                UpdateRecipeLabels(em, entity, bag, slots, 0f);
                if (status != null)
                {
                    if (crewArrived < 1)
                        status.text = crewAssigned > 0
                            ? (dismantling ? "Crew walking to dismantle." : "Crew walking to the site.")
                            : (dismantling ? "Waiting to dismantle." : "Waiting for workers.");
                    else
                        status.text = dismantling
                            ? $"{crewArrived} dismantling."
                            : $"{crewArrived} building.";
                }

                if (workFill != null)
                    workFill.fillAmount = construction.Normalized;
            }
            else if (!onShift)
            {
                UpdateRecipeLabels(em, entity, bag, slots, 0f);
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
                UpdateRecipeLabels(em, entity, bag, slots, 0f);
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
                UpdateRecipeLabels(em, entity, bag, slots, productionLoad);
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
            HudButtons.SetInteractable(plusButton, canStaff && workplace.DesiredWorkers < slots && freeWorkers > 0);
            HudButtons.SetInteractable(minusButton, canStaff && workplace.DesiredWorkers > 0);
            HudButtons.SetInteractable(maxPlusButton, canStaff && workplace.DesiredWorkers < slots && freeWorkers > 0);
            HudButtons.SetInteractable(maxMinusButton, canStaff && workplace.DesiredWorkers > 0);

            HudButtons.SetInteractable(powerButton, !constructing && slots > 0);
            HudButtons.SetInteractable(destroyButton, !dismantling);
            UpdatePowerIcon(paused);
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

        BuildingCatalogAsset FindViewCatalog()
        {
            if (_placement == null)
                _placement = FindFirstObjectByType<BuildPlacementController>();
            return _placement != null ? _placement.Catalog : null;
        }

        void UpdateRecipeLabels(
            EntityManager em,
            Entity buildingEntity,
            Entity session,
            int slots,
            float productionLoad)
        {
            if (buildingRole != null)
                buildingRole.text = slots > 0 ? "Workplace" : "Building";

            if (resourceName == null && resourceRate == null)
                return;

            string name = null;
            string rate = null;
            if (em.HasBuffer<BuildingRecipeLine>(buildingEntity))
            {
                var recipes = em.GetBuffer<BuildingRecipeLine>(buildingEntity);
                var hasNames = em.HasBuffer<ResourceInfo>(session);
                var names = hasNames ? em.GetBuffer<ResourceInfo>(session) : default;
                var chosen = default(BuildingRecipeLine);
                var found = false;
                for (var i = 0; i < recipes.Length; i++)
                {
                    var line = recipes[i];
                    if (line.PerHour <= 0.0001f)
                        continue;
                    if (!found || line.Kind == BuildingRecipeKind.Output)
                    {
                        chosen = line;
                        found = true;
                        if (line.Kind == BuildingRecipeKind.Output)
                            break;
                    }
                }

                if (found)
                {
                    name = hasNames
                        ? DisplayName(names, chosen.ResourceId)
                        : chosen.ResourceId.ToString();
                    var sign = chosen.Kind == BuildingRecipeKind.Input ? "−" : "+";
                    rate = $"{sign}{chosen.PerHour * productionLoad:0.##}";
                }
            }

            if (resourceName != null)
                resourceName.text = name ?? string.Empty;
            if (resourceRate != null)
                resourceRate.text = rate ?? string.Empty;
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

        static void CountWorkforce(EntityManager em, out int totalWorkers, out int totalDesired, out int freeWorkers)
        {
            using var agentQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<AgentId>(),
                ComponentType.ReadOnly<AgentType>());
            using var types = agentQuery.ToComponentDataArray<AgentType>(Allocator.Temp);
            totalWorkers = 0;
            for (var i = 0; i < types.Length; i++)
            {
                if (types[i].Kind == AgentKind.Worker)
                    totalWorkers++;
            }

            using var buildingQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<Workplace>());
            using var workplaces = buildingQuery.ToComponentDataArray<Workplace>(Allocator.Temp);
            totalDesired = 0;
            for (var i = 0; i < workplaces.Length; i++)
            {
                if (!workplaces[i].IsPaused)
                    totalDesired += workplaces[i].DesiredWorkers;
            }

            freeWorkers = Mathf.Max(0, totalWorkers - totalDesired);
        }

        static void CountConstructionCrew(
            EntityManager em,
            int buildingId,
            out int assigned,
            out int arrived)
        {
            assigned = 0;
            arrived = 0;
            if (buildingId <= 0)
                return;

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<AgentAssignment>());
            using var assignments = query.ToComponentDataArray<AgentAssignment>(Allocator.Temp);
            for (var i = 0; i < assignments.Length; i++)
            {
                var job = assignments[i];
                if (job.ConstructionBuildingId != buildingId)
                    continue;
                assigned++;
                if (job.Arrived != 0)
                    arrived++;
            }
        }

        void OnMinus()
        {
            if (selection == null || selection.SelectedBuildingId <= 0)
                return;
            if (!SimWorld.TryGet(out var em, out _) || !TryFindBuilding(em, selection.SelectedBuildingId, out var entity, out _))
                return;
            if (!em.HasComponent<Workplace>(entity))
                return;

            var workplace = em.GetComponentData<Workplace>(entity);
            if (workplace.DesiredWorkers <= 0)
                return;

            workplace.DesiredWorkers--;
            em.SetComponentData(entity, workplace);
            Show(selection.SelectedBuildingId);
        }

        void OnPlus()
        {
            if (selection == null || selection.SelectedBuildingId <= 0)
                return;
            if (!SimWorld.TryGet(out var em, out _) || !TryFindBuilding(em, selection.SelectedBuildingId, out var entity, out _))
                return;
            if (!em.HasComponent<Workplace>(entity))
                return;

            var slots = em.HasComponent<BuildingType>(entity)
                ? em.GetComponentData<BuildingType>(entity).WorkplaceSlots
                : 0;
            var workplace = em.GetComponentData<Workplace>(entity);

            if (workplace.DesiredWorkers >= slots)
                return;

            CountWorkforce(em, out _, out _, out var freeWorkers);
            if (freeWorkers <= 0)
                return;

            workplace.DesiredWorkers++;
            em.SetComponentData(entity, workplace);
            Show(selection.SelectedBuildingId);
        }

        void OnMaxMinus()
        {
            if (selection == null || selection.SelectedBuildingId <= 0)
                return;
            if (!SimWorld.TryGet(out var em, out _) || !TryFindBuilding(em, selection.SelectedBuildingId, out var entity, out _))
                return;
            if (!em.HasComponent<Workplace>(entity))
                return;

            var workplace = em.GetComponentData<Workplace>(entity);
            workplace.DesiredWorkers = 0;
            em.SetComponentData(entity, workplace);
            Show(selection.SelectedBuildingId);
        }

        void OnMaxPlus()
        {
            if (selection == null || selection.SelectedBuildingId <= 0)
                return;
            if (!SimWorld.TryGet(out var em, out _) || !TryFindBuilding(em, selection.SelectedBuildingId, out var entity, out _))
                return;
            if (!em.HasComponent<Workplace>(entity))
                return;

            var slots = em.HasComponent<BuildingType>(entity)
                ? em.GetComponentData<BuildingType>(entity).WorkplaceSlots
                : 0;
            var workplace = em.GetComponentData<Workplace>(entity);

            var needed = slots - workplace.DesiredWorkers;
            if (needed <= 0)
                return;

            CountWorkforce(em, out _, out _, out var freeWorkers);
            if (freeWorkers <= 0)
                return;

            var add = Mathf.Min(needed, freeWorkers);
            workplace.DesiredWorkers += add;
            em.SetComponentData(entity, workplace);
            Show(selection.SelectedBuildingId);
        }

        void OnPower()
        {
            if (selection == null || selection.SelectedBuildingId <= 0)
                return;
            if (!SimWorld.TryGet(out var em, out _) || !TryFindBuilding(em, selection.SelectedBuildingId, out var entity, out _))
                return;
            if (!em.HasComponent<Workplace>(entity))
                return;

            var workplace = em.GetComponentData<Workplace>(entity);
            workplace.Paused = workplace.IsPaused ? (byte)0 : (byte)1;
            em.SetComponentData(entity, workplace);
            Show(selection.SelectedBuildingId);
        }



        void UpdatePowerIcon(bool paused)
        {
            // paused = производство остановлено → показываем Start
            // работает → показываем Stop
            var sprite = paused ? powerStartSprite : powerStopSprite;
            if (sprite == null)
                return;

            var icon = powerIcon;
            if (icon == null && powerButton != null)
                icon = powerButton.GetComponent<Image>();

            if (icon != null)
                icon.sprite = sprite;
        }

        void OnClose()
        {
            selection?.Deselect();
            Hide();
        }

        void OnDestroyBuilding()
        {
            if (selection == null || selection.SelectedBuildingId <= 0)
                return;
            SimCommands.Request(new DemolishBuildingRequest
            {
                BuildingId = selection.SelectedBuildingId
            });
        }

    }
}
