using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Research;
using TheyWillDescend.Simulation.Session;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Frostpunk-style research tree. Reads the research singleton, posts
    /// <see cref="SetActiveResearchCommand"/>. Does not spend stock itself.
    /// </summary>
    public sealed class ResearchWidget : MonoBehaviour
    {
        [SerializeField] Button openButton;
        [SerializeField] GameObject overlay;
        [SerializeField] RectTransform nodeRoot;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text summary;
        [SerializeField] TMP_Text costLabel;
        [SerializeField] TMP_Text timeLabel;
        [SerializeField] TMP_Text status;
        [SerializeField] Image progressFill;
        [SerializeField] Button researchButton;
        [SerializeField] Button closeButton;

        readonly List<NodeView> _nodes = new(8);
        string _selectedId = string.Empty;
        bool _overlayOpen;

        static readonly Color Panel = new(0.08f, 0.09f, 0.11f, 0.96f);
        static readonly Color Ink = new(0.92f, 0.9f, 0.84f, 1f);
        static readonly Color Locked = new(0.22f, 0.22f, 0.24f, 0.95f);
        static readonly Color Available = new(0.2f, 0.32f, 0.42f, 0.95f);
        static readonly Color Active = new(0.62f, 0.48f, 0.18f, 0.98f);
        static readonly Color Done = new(0.22f, 0.42f, 0.28f, 0.98f);
        static readonly Color FillGold = new(0.86f, 0.7f, 0.32f, 1f);

        public static ResearchWidget Current { get; private set; }

        public bool IsBusy => _overlayOpen;

        void Awake()
        {
            Current = this;
            if (openButton == null)
                BuildChrome();
            HudButtons.Bind(openButton, OnOpenClicked);
            HudButtons.Bind(researchButton, OnResearchClicked);
            HudButtons.Bind(closeButton, () => Close(resumeSim: false));
            SetOverlayVisible(false);
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
            HudButtons.Unbind(openButton, OnOpenClicked);
            HudButtons.Unbind(researchButton, OnResearchClicked);
        }

        void Update()
        {
            if (!_overlayOpen)
                return;
            RefreshDetail();
            RefreshNodes();
        }

        public bool TryHandleEscape()
        {
            if (!IsBusy)
                return false;
            Close(resumeSim: false);
            return true;
        }

        public void CloseIfBusy()
        {
            if (IsBusy)
                Close(resumeSim: false);
        }

        void OnOpenClicked()
        {
            if (_overlayOpen)
            {
                Close(resumeSim: false);
                return;
            }

            BuildWidget.Current?.CloseIfBusy();
            RebuildNodes();
            if (_nodes.Count > 0 && string.IsNullOrEmpty(_selectedId))
                _selectedId = _nodes[0].TechId;
            _overlayOpen = true;
            SetOverlayVisible(true);
        }

        void Close(bool resumeSim)
        {
            _overlayOpen = false;
            SetOverlayVisible(false);
        }

        void OnResearchClicked()
        {
            if (string.IsNullOrEmpty(_selectedId))
                return;
            var id = ContentId.EncodeOrEmpty(_selectedId);
            if (id.IsEmpty)
                return;
            if (!SimCommands.TryPost(new SetActiveResearchCommand { TechId = id }))
                GameLog.Warning("Research: command buffer missing — TechCatalogAuthoring not baked.");
        }

        void RebuildNodes()
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].Root != null)
                    Destroy(_nodes[i].Root);
            }

            _nodes.Clear();
            if (nodeRoot == null
                || !TryRead(out _, out _, out _, out _, out _, out var catalog, out _))
                return;

            for (var i = 0; i < catalog.Length; i++)
            {
                var info = catalog[i];
                if (info.TechId.IsEmpty)
                    continue;
                var go = new GameObject($"Tech_{info.TechId}", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(nodeRoot, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(168f, 78f);
                rt.anchoredPosition = new Vector2(110f + info.TreeColumn * 200f, -70f - info.TreeRow * 100f);
                var image = go.GetComponent<Image>();
                image.color = Available;
                var button = go.GetComponent<Button>();
                button.targetGraphic = image;
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(go.transform, false);
                Stretch((RectTransform)labelGo.transform);
                var tmp = labelGo.AddComponent<TextMeshProUGUI>();
                tmp.text = info.DisplayName.ToString();
                tmp.fontSize = 16f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Ink;
                tmp.raycastTarget = false;
                var fillGo = new GameObject("Fill", typeof(RectTransform));
                fillGo.transform.SetParent(go.transform, false);
                var fillRt = (RectTransform)fillGo.transform;
                fillRt.anchorMin = new Vector2(0.08f, 0.08f);
                fillRt.anchorMax = new Vector2(0.92f, 0.18f);
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
                var fill = fillGo.AddComponent<Image>();
                fill.color = FillGold;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.raycastTarget = false;
                var techId = info.TechId.ToString();
                HudButtons.Bind(button, () => _selectedId = techId);
                _nodes.Add(new NodeView
                {
                    TechId = techId,
                    Root = go,
                    Image = image,
                    Fill = fill
                });
            }
        }

        void RefreshNodes()
        {
            if (!TryRead(out var em, out var session, out var research, out var control, out var lines, out var catalog, out var prereqs))
                return;
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                var id = ContentId.EncodeOrEmpty(node.TechId);
                if (!ResearchRules.TryGetInfo(catalog, id, out var info))
                    continue;
                var index = ResearchRules.IndexOf(lines, id);
                var completed = index >= 0 && lines[index].IsCompleted;
                var researching = control.ActiveTechId == id;
                var available = ResearchRules.IsAvailable(info, control, lines, prereqs);
                if (node.Image != null)
                {
                    if (completed)
                        node.Image.color = Done;
                    else if (researching)
                        node.Image.color = Active;
                    else if (available)
                        node.Image.color = Available;
                    else
                        node.Image.color = Locked;
                    if (node.TechId == _selectedId)
                        node.Image.color *= 1.25f;
                }

                if (node.Fill != null)
                {
                    var required = info.RequiredHours > 0.0001f ? info.RequiredHours : 1f;
                    var accumulated = index >= 0 ? lines[index].AccumulatedHours : 0f;
                    node.Fill.fillAmount = completed ? 1f : Mathf.Clamp01(accumulated / required);
                }
            }
        }

        void RefreshDetail()
        {
            if (title == null)
                return;
            if (string.IsNullOrEmpty(_selectedId)
                || !TryRead(out var em, out var session, out var research, out var control, out var lines, out var catalog, out var prereqs)
                || !ResearchRules.TryGetInfo(catalog, ContentId.EncodeOrEmpty(_selectedId), out var info))
            {
                if (title != null)
                    title.text = "Исследования";
                if (summary != null)
                    summary.text = "Выберите технологию.";
                HudButtons.SetInteractable(researchButton, false);
                return;
            }

            var id = info.TechId;
            var index = ResearchRules.IndexOf(lines, id);
            var row = index >= 0 ? lines[index] : default;
            var completed = row.IsCompleted;
            var researching = control.ActiveTechId == id;
            var available = ResearchRules.IsAvailable(info, control, lines, prereqs);
            var hasWorkshop = em.HasComponent<ResearchCapacity>(research)
                && em.GetComponentData<ResearchCapacity>(research).HasWorkshop;
            var stock = em.HasBuffer<ResourceAmount>(session) ? em.GetBuffer<ResourceAmount>(session) : default;
            var costs = em.HasBuffer<TechCatalogCost>(research)
                ? em.GetBuffer<TechCatalogCost>(research)
                : default;
            var names = em.HasBuffer<ResourceInfo>(session) ? em.GetBuffer<ResourceInfo>(session) : default;
            var canAfford = row.IsCostPaid || TechCosts.CanAfford(costs, id, stock);
            var required = info.RequiredHours > 0.0001f ? info.RequiredHours : 1f;

            title.text = info.DisplayName.ToString();
            if (summary != null)
                summary.text = info.Summary.ToString();
            if (costLabel != null)
                costLabel.text = row.IsCostPaid
                    ? "Оплачено"
                    : FormatCost(costs, id, names);
            if (timeLabel != null)
                timeLabel.text = $"{required:0.#} ч";
            if (progressFill != null)
                progressFill.fillAmount = completed ? 1f : Mathf.Clamp01(row.AccumulatedHours / required);

            var canStart = available && hasWorkshop && canAfford && !completed && !researching;
            HudButtons.SetInteractable(researchButton, canStart);
            HudButtons.SetLabel(researchButton, completed ? "Изучено" : researching ? "Изучается" : "Изучить");
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

        static bool TryRead(
            out EntityManager em,
            out Entity session,
            out Entity research,
            out ResearchControl control,
            out DynamicBuffer<ResearchLine> lines,
            out DynamicBuffer<TechInfo> catalog,
            out DynamicBuffer<TechPrerequisite> prereqs)
        {
            control = default;
            lines = default;
            catalog = default;
            prereqs = default;
            session = default;
            research = default;
            em = default;
            if (!SimWorld.TryGet(out em, out session)
                || !SimSessionAccess.TryGetResearch(em, session, out research)
                || !em.HasComponent<ResearchControl>(research)
                || !em.HasBuffer<ResearchLine>(research)
                || !em.HasBuffer<TechInfo>(research))
                return false;
            control = em.GetComponentData<ResearchControl>(research);
            lines = em.GetBuffer<ResearchLine>(research);
            catalog = em.GetBuffer<TechInfo>(research);
            prereqs = em.HasBuffer<TechPrerequisite>(research)
                ? em.GetBuffer<TechPrerequisite>(research)
                : default;
            return true;
        }

        static string FormatCost(
            DynamicBuffer<TechCatalogCost> costs,
            in FixedString64Bytes techId,
            DynamicBuffer<ResourceInfo> names)
        {
            if (!costs.IsCreated || techId.IsEmpty)
                return "Бесплатно";
            var parts = new List<string>(4);
            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i];
                if (cost.TechId != techId || cost.Amount <= 0.0001f)
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

                parts.Add($"{Mathf.CeilToInt(cost.Amount)} {name}");
            }

            return parts.Count == 0 ? "Бесплатно" : string.Join(", ", parts);
        }

        void SetOverlayVisible(bool visible)
        {
            if (overlay != null)
                overlay.SetActive(visible);
        }

        void BuildChrome()
        {
            Transform host = transform;
            if (GetComponent<Canvas>() != null)
            {
                var holder = new GameObject("ResearchHud", typeof(RectTransform));
                holder.transform.SetParent(transform, false);
                Stretch((RectTransform)holder.transform);
                host = holder.transform;
            }
            else
            {
                var rt = GetComponent<RectTransform>();
                if (rt == null)
                    rt = gameObject.AddComponent<RectTransform>();
                Stretch(rt);
            }

            openButton = CreateButton(host, "ResearchOpen", "Исследования", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(168f, 48f), new Vector2(24f, 58f));
            overlay = new GameObject("ResearchOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(host, false);
            var overlayRt = (RectTransform)overlay.transform;
            Stretch(overlayRt);
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);

            nodeRoot = new GameObject("Tree", typeof(RectTransform)).GetComponent<RectTransform>();
            nodeRoot.SetParent(overlay.transform, false);
            nodeRoot.anchorMin = new Vector2(0.04f, 0.12f);
            nodeRoot.anchorMax = new Vector2(0.62f, 0.92f);
            nodeRoot.offsetMin = Vector2.zero;
            nodeRoot.offsetMax = Vector2.zero;

            var card = new GameObject("Detail", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(overlay.transform, false);
            var cardRt = (RectTransform)card.transform;
            cardRt.anchorMin = new Vector2(0.66f, 0.12f);
            cardRt.anchorMax = new Vector2(0.96f, 0.92f);
            cardRt.offsetMin = Vector2.zero;
            cardRt.offsetMax = Vector2.zero;
            card.GetComponent<Image>().color = Panel;

            var layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 18, 18);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            title = CreateText(card.transform, "Title", "Исследования", 22f, 36f);
            summary = CreateText(card.transform, "Summary", "", 16f, 96f);
            costLabel = CreateText(card.transform, "Cost", "", 16f, 28f);
            timeLabel = CreateText(card.transform, "Time", "", 16f, 28f);

            var bar = new GameObject("Progress", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(card.transform, false);
            bar.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.95f);
            var barLayout = bar.AddComponent<LayoutElement>();
            barLayout.minHeight = 14f;
            barLayout.preferredHeight = 14f;
            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(bar.transform, false);
            Stretch((RectTransform)fillGo.transform);
            progressFill = fillGo.AddComponent<Image>();
            progressFill.color = FillGold;
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.raycastTarget = false;

            status = CreateText(card.transform, "Status", "", 15f, 48f);
            researchButton = CreateLayoutButton(card.transform, "Изучить", 48f);
            closeButton = CreateButton(overlay.transform, "ResearchClose", "Закрыть", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(120f, 40f), new Vector2(-24f, -24f));
            overlay.transform.SetAsLastSibling();
        }

        static TMP_Text CreateText(Transform parent, string name, string value, float size, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = value;
            tmp.fontSize = size;
            tmp.color = Ink;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        static Button CreateLayoutButton(Transform parent, string label, float height)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            var image = go.GetComponent<Image>();
            image.color = new Color(0.22f, 0.38f, 0.48f, 0.96f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch((RectTransform)textGo.transform);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return button;
        }

        static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 anchored)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchored;
            var image = go.GetComponent<Image>();
            image.color = new Color(0.16f, 0.18f, 0.22f, 0.94f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch((RectTransform)textGo.transform);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Ink;
            tmp.raycastTarget = false;
            return button;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        struct NodeView
        {
            public string TechId;
            public GameObject Root;
            public Image Image;
            public Image Fill;
        }
    }
}
