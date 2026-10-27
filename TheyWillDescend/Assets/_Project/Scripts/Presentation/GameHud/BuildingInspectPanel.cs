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
using VContainer;

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
        string _lastTitle;
        string _lastWorkers;
        string _lastIdle;
        string _lastStatus;
        string _lastRole;
        string _lastResourceName;
        string _lastResourceRate;

        [Inject]
        public void Construct(BuildPlacementController placement)
        {
            _placement = placement;
        }

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

            var house = selection.Selected;
            if (house == Entity.Null)
            {
                Hide();
                return;
            }

            Show(house);
        }

        void Hide()
        {
            if (card != null)
                card.SetActive(false);
        }

        void Show(Entity entity)
        {
            if (!SimWorld.TryGet(out var em, out var bag)
                || !em.Exists(entity)
                || !em.HasComponent<Building>(entity))
            {
                selection?.ClearIf(entity);
                Hide();
                return;
            }

            var building = em.GetComponentData<Building>(entity);

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
            Write(title, ref _lastTitle, string.IsNullOrEmpty(displayName) ? $"Building {building.TypeId}" : displayName);

            var occupied = workplace.AssignedCount;
            var working = workplace.WorkingCount;
            var paused = workplace.IsPaused;
            var onShift = SimWorld.TryGetGameTime(em, bag, out var time) && time.IsWorkShift;
            if (slots < 0)
                slots = 0;
            var summary = em.HasComponent<WorkforceSummary>(bag)
                ? em.GetComponentData<WorkforceSummary>(bag)
                : default;
            Write(workers, ref _lastWorkers, $"{occupied} / {workplace.DesiredWorkers}");
            Write(idle, ref _lastIdle, $"Idle workers  {summary.IdleWorkers}");

            if (constructing)
            {
                var crewAssigned = construction.CrewAssigned;
                var crewArrived = construction.CrewArrived;
                Write(workers, ref _lastWorkers, $"{construction.Normalized * 100f:0}%");
                UpdateRecipeLabels(em, entity, bag, slots, 0f);
                Write(status, ref _lastStatus, crewArrived < 1
                    ? crewAssigned > 0
                        ? (dismantling ? "Crew walking to dismantle." : "Crew walking to the site.")
                        : (dismantling ? "Waiting to dismantle." : "Waiting for workers.")
                    : dismantling
                        ? $"{crewArrived} dismantling."
                        : $"{crewArrived} building.");

                if (workFill != null)
                    workFill.fillAmount = construction.Normalized;
            }
            else if (!onShift)
            {
                UpdateRecipeLabels(em, entity, bag, slots, 0f);
                Write(status, ref _lastStatus, occupied == 0
                    ? $"Off shift ({time.WorkShiftStartHour:00}:00–{time.WorkShiftEndHour:00}:00). No one assigned."
                    : paused
                        ? "Off shift. Crew at the pyramid. Production is stopped."
                        : "Off shift. Crew walks around the pyramid.");

                if (workFill != null)
                    workFill.fillAmount = Workplace.Load01(occupied, slots);
            }
            else if (paused)
            {
                UpdateRecipeLabels(em, entity, bag, slots, 0f);
                Write(status, ref _lastStatus, occupied == 0
                    ? "Production stopped. No one assigned."
                    : "Production stopped. Crew stays assigned.");
                if (workFill != null)
                    workFill.fillAmount = Workplace.Load01(occupied, slots);
            }
            else
            {
                var productionLoad = Workplace.Load01(working, slots);
                UpdateRecipeLabels(em, entity, bag, slots, productionLoad);
                Write(status, ref _lastStatus, occupied == 0
                    ? "No one assigned."
                    : working >= occupied
                        ? "All workers on site."
                        : $"{working} on site, {occupied - working} walking.");

                if (workFill != null)
                    workFill.fillAmount = Workplace.Load01(occupied, slots);
            }

            var canStaff = !constructing && slots > 0;
            HudButtons.SetInteractable(plusButton, canStaff && workplace.DesiredWorkers < slots);
            HudButtons.SetInteractable(minusButton, canStaff && workplace.DesiredWorkers > 0);
            HudButtons.SetInteractable(maxPlusButton, canStaff && workplace.DesiredWorkers < slots);
            HudButtons.SetInteractable(maxMinusButton, canStaff && workplace.DesiredWorkers > 0);

            HudButtons.SetInteractable(powerButton, !constructing && slots > 0);
            HudButtons.SetInteractable(destroyButton, !dismantling);
            UpdatePowerIcon(paused);
        }

        BuildingCatalogAsset FindViewCatalog()
        {
            return _placement != null ? _placement.Catalog : null;
        }

        static void Write(TMP_Text label, ref string last, string value)
        {
            if (label == null || last == value)
                return;
            last = value;
            label.text = value;
        }

        void UpdateRecipeLabels(
            EntityManager em,
            Entity buildingEntity,
            Entity session,
            int slots,
            float productionLoad)
        {
            Write(buildingRole, ref _lastRole, slots > 0 ? "Workplace" : "Building");

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

            Write(resourceName, ref _lastResourceName, name ?? string.Empty);
            Write(resourceRate, ref _lastResourceRate, rate ?? string.Empty);
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

        bool TrySelectedWorkplace(out EntityManager em, out Entity entity, out Workplace workplace)
        {
            em = default;
            entity = Entity.Null;
            workplace = default;
            if (selection == null || selection.Selected == Entity.Null)
                return false;
            if (!SimWorld.TryGet(out em, out _))
                return false;
            entity = selection.Selected;
            if (!em.Exists(entity) || !em.HasComponent<Workplace>(entity))
                return false;
            workplace = em.GetComponentData<Workplace>(entity);
            return true;
        }

        void OnMinus()
        {
            if (!TrySelectedWorkplace(out var em, out var entity, out var workplace))
                return;
            if (workplace.DesiredWorkers <= 0)
                return;

            workplace.DesiredWorkers--;
            em.SetComponentData(entity, workplace);
            Show(entity);
        }

        void OnPlus()
        {
            if (!TrySelectedWorkplace(out var em, out var entity, out var workplace))
                return;

            var slots = em.HasComponent<BuildingType>(entity)
                ? em.GetComponentData<BuildingType>(entity).WorkplaceSlots
                : 0;
            if (workplace.DesiredWorkers >= slots)
                return;

            workplace.DesiredWorkers++;
            em.SetComponentData(entity, workplace);
            Show(entity);
        }

        void OnMaxMinus()
        {
            if (!TrySelectedWorkplace(out var em, out var entity, out var workplace))
                return;

            workplace.DesiredWorkers = 0;
            em.SetComponentData(entity, workplace);
            Show(entity);
        }

        void OnMaxPlus()
        {
            if (!TrySelectedWorkplace(out var em, out var entity, out var workplace))
                return;

            var slots = em.HasComponent<BuildingType>(entity)
                ? em.GetComponentData<BuildingType>(entity).WorkplaceSlots
                : 0;
            if (workplace.DesiredWorkers >= slots)
                return;

            workplace.DesiredWorkers = slots;
            em.SetComponentData(entity, workplace);
            Show(entity);
        }

        void OnPower()
        {
            if (!TrySelectedWorkplace(out var em, out var entity, out var workplace))
                return;

            workplace.Paused = workplace.IsPaused ? (byte)0 : (byte)1;
            em.SetComponentData(entity, workplace);
            Show(entity);
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
            if (selection == null || selection.Selected == Entity.Null)
                return;
            SimCommands.Request(new DemolishBuildingRequest
            {
                Building = selection.Selected
            });
        }
    }
}
