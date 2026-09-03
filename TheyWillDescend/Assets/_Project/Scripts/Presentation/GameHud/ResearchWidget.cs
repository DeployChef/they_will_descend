using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Research;
using TheyWillDescend.Simulation.Session;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Research catalog lives on the canvas like <see cref="BuildWidget"/>.
    /// <see cref="researchModeButton"/> opens <see cref="researchPanel"/>.
    /// </summary>
    public sealed class ResearchWidget : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("openButton")] Button researchModeButton;
        [SerializeField, FormerlySerializedAs("overlay")] GameObject researchPanel;
        [SerializeField] ResearchNodeView[] nodes;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text summary;
        [SerializeField] TMP_Text costLabel;
        [SerializeField] TMP_Text timeLabel;
        [SerializeField] TMP_Text status;
        [SerializeField] Image progressFill;
        [SerializeField] Image modeButtonProgress;
        [SerializeField, FormerlySerializedAs("researchButton")] Button studyButton;
        [SerializeField] Button closeButton;

        string _selectedId = string.Empty;
        bool _panelOpen;
        bool _bound;

        static readonly Color Locked = new(0.22f, 0.22f, 0.24f, 0.95f);
        static readonly Color Available = new(0.2f, 0.32f, 0.42f, 0.95f);
        static readonly Color Active = new(0.62f, 0.48f, 0.18f, 0.98f);
        static readonly Color Done = new(0.22f, 0.42f, 0.28f, 0.98f);

        public static ResearchWidget Current { get; private set; }

        public bool IsBusy => _panelOpen;

        void Awake()
        {
            Current = this;
            if (researchModeButton == null || researchPanel == null || nodes == null || nodes.Length == 0)
            {
                GameLog.Error("ResearchWidget: assign ResearchButton and ResearchPanel on the canvas. UI is not spawned from code.");
                enabled = false;
                return;
            }

            HudButtons.Bind(researchModeButton, OnOpenClicked);
            HudButtons.Bind(studyButton, OnResearchClicked);
            HudButtons.Bind(closeButton, OnCloseClicked);
            BindNodes();
            HideAllNodes();
            SetPanelVisible(false);
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
            if (!_bound)
                return;
            HudButtons.Unbind(researchModeButton, OnOpenClicked);
            HudButtons.Unbind(studyButton, OnResearchClicked);
            HudButtons.Unbind(closeButton, OnCloseClicked);
            UnbindNodes();
        }

        void Update()
        {
            RefreshModeButtonProgress();
            if (!_panelOpen)
                return;
            RefreshDetail();
            RefreshNodes();
        }

        public bool TryHandleEscape()
        {
            if (!IsBusy)
                return false;
            Close();
            return true;
        }

        public void CloseIfBusy()
        {
            if (IsBusy)
                Close();
        }

        void BindNodes()
        {
            _bound = true;
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null || node.Button == null)
                    continue;
                var captured = node;
                HudButtons.Bind(captured.Button, () => Select(captured));
            }
        }

        void UnbindNodes()
        {
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null || node.Button == null)
                    continue;
                node.Button.onClick.RemoveAllListeners();
            }
        }

        void Select(ResearchNodeView node)
        {
            if (node == null || string.IsNullOrEmpty(node.TechId))
                return;
            _selectedId = node.TechId;
            RefreshDetail();
            RefreshNodes();
        }

        void OnCloseClicked() => Close();

        void OnOpenClicked()
        {
            if (_panelOpen)
            {
                Close();
                return;
            }

            BuildWidget.Current?.CloseIfBusy();
            BindCards();
            _panelOpen = true;
            SetPanelVisible(true);
            RefreshDetail();
            RefreshNodes();
        }

        void Close()
        {
            _panelOpen = false;
            SetPanelVisible(false);
        }

        void OnResearchClicked()
        {
            if (string.IsNullOrEmpty(_selectedId))
                return;
            var id = ContentId.EncodeOrEmpty(_selectedId);
            if (id.IsEmpty)
                return;
            if (!SimCommands.TryPost(new SetActiveResearchCommand { TechId = id }))
                GameLog.Warning("Research: command buffer missing — run catalog was not populated.");
        }

        void HideAllNodes()
        {
            if (nodes == null)
                return;
            for (var i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null)
                    nodes[i].gameObject.SetActive(false);
            }
        }

        void BindCards()
        {
            HideAllNodes();
            _selectedId = string.Empty;
            if (!SimWorld.TryGet(out var em, out _))
            {
                GameLog.Warning("Research HUD: simulation world is not ready.");
                return;
            }

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<TechInfo>());
            using var infos = query.ToComponentDataArray<TechInfo>(Allocator.Temp);
            if (infos.Length == 0)
            {
                GameLog.Warning("Research HUD: no tech cards in the world.");
                return;
            }

            var used = 0;
            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                if (info.TechId.IsEmpty)
                    continue;
                if (used >= nodes.Length)
                {
                    GameLog.Warning("Research HUD: more techs than prefab node slots.");
                    break;
                }

                var node = nodes[used++];
                if (node == null)
                    continue;
                node.TechId = info.TechId.ToString();
                if (node.Label != null)
                    node.Label.text = info.DisplayName.ToString();
                var rt = node.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(110f + info.TreeColumn * 200f, -70f - info.TreeRow * 100f);
                }

                node.gameObject.SetActive(true);
            }

            if (used > 0 && nodes[0] != null)
                _selectedId = nodes[0].TechId;
        }

        void RefreshNodes()
        {
            if (!TryReadBoard(out var em, out _, out _, out var control) || nodes == null)
                return;
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null || !node.gameObject.activeSelf || string.IsNullOrEmpty(node.TechId))
                    continue;
                var id = ContentId.EncodeOrEmpty(node.TechId);
                if (!ResearchWorld.TryFindCard(em, id, out _, out var info, out var progress))
                    continue;
                var completed = progress.IsCompleted;
                var researching = control.ActiveTechId == id;
                var available = ResearchRules.IsAvailable(em, info, progress, control);
                if (node.Background != null)
                {
                    if (completed)
                        node.Background.color = Done;
                    else if (researching)
                        node.Background.color = Active;
                    else if (available)
                        node.Background.color = Available;
                    else
                        node.Background.color = Locked;
                    if (node.TechId == _selectedId)
                        node.Background.color *= 1.25f;
                }

                if (node.Fill != null)
                {
                    var required = info.RequiredHours > 0.0001f ? info.RequiredHours : 1f;
                    node.Fill.fillAmount = completed ? 1f : Mathf.Clamp01(progress.AccumulatedHours / required);
                    node.Fill.color = completed
                        ? new Color(0.45f, 0.78f, 0.52f, 1f)
                        : researching
                            ? new Color(0.92f, 0.74f, 0.28f, 1f)
                            : new Color(0.72f, 0.68f, 0.52f, 0.85f);
                }
            }
        }

        void RefreshDetail()
        {
            if (title == null)
                return;
            if (string.IsNullOrEmpty(_selectedId)
                || !TryReadBoard(out var em, out var session, out var board, out var control)
                || !ResearchWorld.TryFindCard(em, ContentId.EncodeOrEmpty(_selectedId), out var card, out var info, out var progress))
            {
                title.text = "Исследования";
                if (summary != null)
                    summary.text = "Выберите технологию.";
                HudButtons.SetInteractable(studyButton, false);
                return;
            }

            var id = info.TechId;
            var completed = progress.IsCompleted;
            var researching = control.ActiveTechId == id;
            var available = ResearchRules.IsAvailable(em, info, progress, control);
            var hasWorkshop = em.HasComponent<ResearchCapacity>(board)
                && em.GetComponentData<ResearchCapacity>(board).HasWorkshop;
            var stock = em.HasBuffer<ResourceAmount>(session) ? em.GetBuffer<ResourceAmount>(session) : default;
            var costs = em.HasBuffer<TechCatalogCost>(card)
                ? em.GetBuffer<TechCatalogCost>(card)
                : default;
            var names = em.HasBuffer<ResourceInfo>(session) ? em.GetBuffer<ResourceInfo>(session) : default;
            var canAfford = progress.IsCostPaid || TechCosts.CanAfford(costs, stock);
            var required = info.RequiredHours > 0.0001f ? info.RequiredHours : 1f;

            title.text = info.DisplayName.ToString();
            if (summary != null)
                summary.text = info.Summary.ToString();
            if (costLabel != null)
                costLabel.text = progress.IsCostPaid ? "Оплачено" : FormatCost(costs, names);
            if (timeLabel != null)
                timeLabel.text = required.ToString("0.#") + " ч";
            if (progressFill != null)
                progressFill.fillAmount = completed ? 1f : Mathf.Clamp01(progress.AccumulatedHours / required);

            var canStart = available && hasWorkshop && canAfford && !completed && !researching;
            HudButtons.SetInteractable(studyButton, canStart);
            HudButtons.SetLabel(studyButton, completed ? "Изучено" : researching ? "Изучается" : "Изучить");
            if (status != null)
            {
                if (completed)
                    status.text = "Изучено.";
                else if (researching)
                    status.text = "Мастерская работает над этим.";
                else if (!available)
                    status.text = "Сначала откройте предыдущий уровень или технологию.";
                else if (!hasWorkshop)
                    status.text = "Нужна построенная мастерская.";
                else if (!canAfford)
                    status.text = "Не хватает ресурсов.";
                else
                    status.text = "Можно изучать.";
            }
        }

        static bool TryReadBoard(
            out EntityManager em,
            out Entity session,
            out Entity board,
            out ResearchControl control)
        {
            control = default;
            session = default;
            board = default;
            em = default;
            if (!SimWorld.TryGet(out em, out session)
                || !ResearchWorld.TryGetBoard(em, out board)
                || !em.HasComponent<ResearchControl>(board))
                return false;
            control = em.GetComponentData<ResearchControl>(board);
            return true;
        }

        static string FormatCost(
            DynamicBuffer<TechCatalogCost> costs,
            DynamicBuffer<ResourceInfo> names)
        {
            if (!costs.IsCreated)
                return "Бесплатно";
            var parts = new System.Collections.Generic.List<string>(4);
            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i];
                if (cost.Amount <= 0.0001f)
                    continue;
                var name = cost.ResourceId.ToString();
                if (names.IsCreated)
                {
                    for (var n = 0; n < names.Length; n++)
                    {
                        if (names[n].ResourceId != cost.ResourceId)
                            continue;
                        name = names[n].DisplayName.ToString();
                        break;
                    }
                }

                parts.Add(Mathf.CeilToInt(cost.Amount) + " " + name);
            }

            return parts.Count == 0 ? "Бесплатно" : string.Join(", ", parts);
        }

        void RefreshModeButtonProgress()
        {
            if (modeButtonProgress == null)
                return;
            if (!TryReadBoard(out var em, out _, out _, out var control)
                || control.ActiveTechId.IsEmpty
                || !ResearchWorld.TryFindCard(em, control.ActiveTechId, out _, out var info, out var progress))
            {
                modeButtonProgress.fillAmount = 0f;
                return;
            }

            var required = info.RequiredHours > 0.0001f ? info.RequiredHours : 1f;
            modeButtonProgress.fillAmount = progress.IsCompleted
                ? 1f
                : Mathf.Clamp01(progress.AccumulatedHours / required);
        }

        void SetPanelVisible(bool visible)
        {
            if (researchPanel != null)
                researchPanel.SetActive(visible);
        }
    }
}
