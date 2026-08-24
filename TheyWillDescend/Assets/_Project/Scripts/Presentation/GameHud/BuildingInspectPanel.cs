using TheyWillDescend.Presentation.City;
using TheyWillDescend.Simulation.Io;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Right-dock building card. Pulls workplace and catalog name; sends assign commands.
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
            if (!SimIo.TryGetBuildingInspect(id, out var inspect))
            {
                Hide();
                return;
            }

            if (card != null)
                card.SetActive(true);
            if (title != null)
                title.text = inspect.DisplayName;

            var occupied = inspect.Workplace.WorkerAgentId != 0 ? 1 : 0;
            var slots = inspect.WorkplaceSlots > 0 ? inspect.WorkplaceSlots : 1;
            var idleCount = SimIo.CountIdleWorkers();
            if (workers != null)
                workers.text = $"{occupied} / {slots}";
            if (idle != null)
                idle.text = $"Idle workers  {idleCount}";

            if (inspect.Constructing)
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
                    subtitle.text = inspect.WorkplaceSlots > 0 ? "Workplace" : "Building";
                if (status != null)
                    status.text = occupied == 0
                        ? "No one assigned."
                        : inspect.Workplace.Working != 0
                            ? "Worker on site."
                            : "Worker walking in.";
                if (workFill != null)
                    workFill.fillAmount = occupied == 0 ? 0f : 1f;
            }

            var canAssign = !inspect.Constructing && inspect.WorkplaceSlots > 0;
            HudButtons.SetInteractable(plusButton, canAssign && occupied == 0 && idleCount > 0);
            HudButtons.SetInteractable(minusButton, canAssign && occupied != 0);
        }

        void OnMinus()
        {
            if (board != null && board.SelectedBuildingId > 0)
                SimIo.TryEnqueueUnassignWorker(board.SelectedBuildingId);
        }

        void OnPlus()
        {
            if (board != null && board.SelectedBuildingId > 0)
                SimIo.TryEnqueueAssignWorker(board.SelectedBuildingId);
        }

        void OnClose()
        {
            board?.Deselect();
            Hide();
        }
    }
}
