using TheyWillDescend.Presentation.City;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
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
        [SerializeField] GameObject card;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text workers;
        [SerializeField] TMP_Text idle;
        [SerializeField] TMP_Text status;
        [SerializeField] Button minusButton;
        [SerializeField] Button plusButton;
        [SerializeField] Button closeButton;
        [SerializeField] Image workFill;

        void Awake()
        {
            HudButtons.Bind(minusButton, OnMinus);
            HudButtons.Bind(plusButton, OnPlus);
            HudButtons.Bind(closeButton, OnClose);
            if (Application.isPlaying)
                Hide();
        }

        void OnDestroy()
        {
            HudButtons.Unbind(minusButton, OnMinus);
            HudButtons.Unbind(plusButton, OnPlus);
            HudButtons.Unbind(closeButton, OnClose);
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

            var constructing = em.HasComponent<Construction>(entity);
            var workplace = em.HasComponent<Workplace>(entity)
                ? em.GetComponentData<Workplace>(entity)
                : default;
            var displayName = building.TypeId.ToString();
            var slots = em.HasComponent<BuildingType>(entity)
                ? em.GetComponentData<BuildingType>(entity).WorkplaceSlots
                : 0;
            if (em.HasBuffer<BuildingPrototype>(bag)
                && BuildingCatalog.TryResolve(
                    em.GetBuffer<BuildingPrototype>(bag),
                    building.TypeId,
                    0,
                    0,
                    out var prototype))
            {
                displayName = prototype.DisplayName.ToString();
                if (slots <= 0)
                    slots = prototype.WorkplaceSlots;
            }

            if (card != null)
                card.SetActive(true);
            if (title != null)
                title.text = string.IsNullOrEmpty(displayName) ? $"Building {building.TypeId}" : displayName;

            var occupied = workplace.AssignedCount;
            var working = workplace.WorkingCount;
            if (slots < 0)
                slots = 0;
            var idleCount = CountIdleWorkers(em);
            if (workers != null)
                workers.text = $"{occupied} / {slots}";
            if (idle != null)
                idle.text = $"Idle workers  {idleCount}";

            if (constructing)
            {
                if (subtitle != null)
                    subtitle.text = "Under construction";
                if (status != null)
                    status.text = "Crew locked until the house stands.";
                if (workFill != null)
                    workFill.fillAmount = 0.35f;
            }
            else
            {
                var productionLoad = Workplace.Load01(working, slots);
                if (subtitle != null)
                    subtitle.text = FormatRecipeSubtitle(em, bag, building.TypeId, slots, productionLoad);
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

            var canAssign = !constructing && slots > 0;
            HudButtons.SetInteractable(plusButton, canAssign && occupied < slots && idleCount > 0);
            HudButtons.SetInteractable(minusButton, canAssign && occupied > 0);
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

        static string FormatRecipeSubtitle(
            EntityManager em,
            Entity session,
            FixedString64Bytes typeId,
            int slots,
            float productionLoad)
        {
            var role = slots > 0 ? "Workplace" : "Building";
            if (!em.HasBuffer<BuildingRecipeLine>(session))
                return role;

            var recipes = em.GetBuffer<BuildingRecipeLine>(session);
            var hasNames = em.HasBuffer<ResourceInfo>(session);
            var names = hasNames ? em.GetBuffer<ResourceInfo>(session) : default;
            var parts = new System.Text.StringBuilder(role);
            var any = false;
            for (var i = 0; i < recipes.Length; i++)
            {
                var line = recipes[i];
                if (line.TypeId != typeId || line.PerHour <= 0.0001f)
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

        void OnClose()
        {
            selection?.Deselect();
            Hide();
        }
    }
}
