using TheyWillDescend.Presentation.City;
using TheyWillDescend.Simulation.Io;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Right-dock building card. Scene-authored. Pulls workplace; sends assign commands.
    /// </summary>
    public sealed class BuildingInspectPanel : MonoBehaviour
    {
        static readonly string[] Titles =
        {
            "Timber Yard",
            "Cookhouse",
            "Hunter's Hut",
            "Gathering Post",
            "Workshop",
            "Sawmill",
            "Charcoal Kiln",
            "Infirmary"
        };

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
            if (card != null)
                card.SetActive(true);
            if (title != null)
                title.text = TitleFor(id);

            var known = SimIo.TryGetWorkplace(id, out var workplace, out var constructing);
            var occupied = known && workplace.WorkerAgentId != 0 ? 1 : 0;
            var idleCount = SimIo.CountIdleWorkers();
            if (workers != null)
                workers.text = $"{occupied} / 1";
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
                    subtitle.text = "House";
                if (status != null)
                    status.text = occupied == 0
                        ? "No one assigned."
                        : workplace.Working != 0
                            ? "Worker on site."
                            : "Worker walking in.";
                if (workFill != null)
                    workFill.fillAmount = occupied == 0 ? 0f : 1f;
            }

            HudButtons.SetInteractable(plusButton, !constructing && occupied == 0 && idleCount > 0);
            HudButtons.SetInteractable(minusButton, !constructing && occupied != 0);
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

        static string TitleFor(int buildingId)
        {
            var index = (int)((uint)buildingId * 2654435761u % (uint)Titles.Length);
            return Titles[index];
        }
    }
}
