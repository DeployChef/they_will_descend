using TheyWillDescend.Presentation.City;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
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
        [SerializeField] BuildingViewBoard board;
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
            if (board == null)
                return;

            var id = board.SelectedBuildingId;
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
            var slots = em.HasComponent<Workplace>(entity) ? 1 : 0;
            if (em.HasBuffer<BuildingPrototype>(bag)
                && BuildingCatalog.TryResolve(
                    em.GetBuffer<BuildingPrototype>(bag),
                    building.TypeId,
                    0,
                    0,
                    out var prototype))
            {
                displayName = prototype.DisplayName.ToString();
                if (prototype.WorkplaceSlots > 0)
                    slots = prototype.WorkplaceSlots;
            }

            if (card != null)
                card.SetActive(true);
            if (title != null)
                title.text = string.IsNullOrEmpty(displayName) ? $"Building {building.TypeId}" : displayName;

            var occupied = workplace.WorkerAgentId != 0 ? 1 : 0;
            if (slots <= 0)
                slots = 1;
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
                if (subtitle != null)
                    subtitle.text = slots > 0 ? "Workplace" : "Building";
                if (status != null)
                    status.text = occupied == 0
                        ? "No one assigned."
                        : workplace.Working != 0
                            ? "Worker on site."
                            : "Worker walking in.";
                if (workFill != null)
                    workFill.fillAmount = occupied == 0 ? 0f : 1f;
            }

            var canAssign = !constructing && slots > 0;
            HudButtons.SetInteractable(plusButton, canAssign && occupied == 0 && idleCount > 0);
            HudButtons.SetInteractable(minusButton, canAssign && occupied != 0);
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
            if (board != null && board.SelectedBuildingId > 0)
                SimCommands.TryPost(new UnassignWorkerCommand { BuildingId = board.SelectedBuildingId });
        }

        void OnPlus()
        {
            if (board != null && board.SelectedBuildingId > 0)
                SimCommands.TryPost(new AssignWorkerCommand
                {
                    BuildingId = board.SelectedBuildingId
                });
        }

        void OnClose()
        {
            board?.Deselect();
            Hide();
        }
    }
}
