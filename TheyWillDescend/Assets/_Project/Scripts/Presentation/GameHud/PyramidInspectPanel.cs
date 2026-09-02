using TheyWillDescend.Presentation.City;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Gods;
using TheyWillDescend.Simulation.Session;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// HQ card: per-resource burn sliders. Posts <see cref="SetPyramidFeedCommand"/>.
    /// </summary>
    public sealed class PyramidInspectPanel : MonoBehaviour
    {
        // Slider needs a finite visual range, but feed itself has no domain cap.
        // Grow the presentation range whenever a value approaches its edge.
        const float InitialFeedSoftRange = 20f;
        const float SoftRangeExpansionThreshold = 0.8f;
        const float SoftRangeExpansionFactor = 2f;

        [SerializeField] BuildingSelection selection;
        [SerializeField] GameObject card;
        public BuildingSelection Selection => selection;

        public void Bind(BuildingSelection nextSelection, GameObject nextCard)
        {
            selection = nextSelection;
            card = nextCard;
        }

        [SerializeField] Transform sliderRoot;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text status;
        [SerializeField] Button closeButton;

        readonly System.Collections.Generic.List<SliderRow> rows = new();
        bool suppress;

        struct SliderRow
        {
            public FixedString64Bytes ResourceId;
            public Slider Slider;
            public TMP_Text Label;
        }

        static readonly string[] HouseChrome =
        {
            "Portrait", "Workers", "Idle", "WorkBar",
            "MinusButton", "PlusButton", "MaxMinusButton", "MaxPlusButton", "PowerButton"
        };

        void Awake()
        {
            HudButtons.Bind(closeButton, OnClose);
            EnsureUi();
            if (Application.isPlaying)
                Hide();
        }

        void OnDestroy()
        {
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
            if (!SimWorld.TryGet(out var em, out var bag)
                || !TryFindHq(em, id, out var entity)
                || !em.HasBuffer<PyramidFeedLine>(entity)
                || !em.HasBuffer<ResourceInfo>(bag))
            {
                Hide();
                return;
            }

            EnsureUi();
            if (card != null)
                card.SetActive(true);

            var feed = em.GetBuffer<PyramidFeedLine>(entity);
            var info = em.GetBuffer<ResourceInfo>(bag);
            var tribute = em.HasBuffer<EraTributeLine>(bag) ? em.GetBuffer<EraTributeLine>(bag) : default;
            var eras = em.HasBuffer<EraLine>(bag) ? em.GetBuffer<EraLine>(bag) : default;
            var eraIndex = em.HasComponent<Timeline>(bag) ? em.GetComponentData<Timeline>(bag).EraIndex : 0;
            var used = tribute.IsCreated && eras.IsCreated
                ? PyramidFeed.TotalEnergyPerHour(feed, info, tribute, eras, eraIndex)
                : 0f;

            if (title != null)
                title.text = "Пирамида";
            if (subtitle != null)
                subtitle.text = $"Жертва  {used:0.#} энергии/ч";

            SyncRows(feed, info, tribute, eraIndex);
            if (status != null)
                status.text = FormatStatus(feed, info, tribute, eras, eraIndex);
        }

        void SyncRows(
            DynamicBuffer<PyramidFeedLine> feed,
            DynamicBuffer<ResourceInfo> info,
            DynamicBuffer<EraTributeLine> tribute,
            int eraIndex)
        {
            EnsureRows(feed, info);
            suppress = true;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var index = PyramidFeed.IndexOf(feed, row.ResourceId);
                var perHour = index >= 0 ? feed[index].PerHour : 0f;
                if (row.Slider != null)
                {
                    row.Slider.minValue = 0f;
                    row.Slider.maxValue = ExpandedSoftRange(row.Slider.maxValue, perHour);
                    row.Slider.SetValueWithoutNotify(perHour);
                    row.Slider.interactable = true;
                }

                if (row.Label != null)
                {
                    var name = DisplayName(info, row.ResourceId);
                    var gift = tribute.IsCreated && PyramidFeed.IsTribute(tribute, eraIndex, row.ResourceId)
                        ? "  ·  tribute"
                        : "";
                    row.Label.text = $"{name}  {perHour:0.#}/h{gift}";
                }
            }

            suppress = false;
        }

        void EnsureRows(DynamicBuffer<PyramidFeedLine> feed, DynamicBuffer<ResourceInfo> info)
        {
            EnsureUi();
            if (sliderRoot == null)
                return;
            if (rows.Count == feed.Length)
                return;

            for (var i = sliderRoot.childCount - 1; i >= 0; i--)
                Destroy(sliderRoot.GetChild(i).gameObject);
            rows.Clear();
            for (var i = 0; i < feed.Length; i++)
            {
                var id = feed[i].ResourceId;
                rows.Add(BuildRow(id, DisplayName(info, id)));
            }
        }

        SliderRow BuildRow(FixedString64Bytes resourceId, string name)
        {
            var go = new GameObject($"Feed_{name}", typeof(RectTransform));
            go.transform.SetParent(sliderRoot, false);
            var rowLayout = go.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 4f;
            rowLayout.padding = new RectOffset(0, 0, 2, 6);
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            var rowSize = go.AddComponent<LayoutElement>();
            rowSize.minHeight = 56f;
            rowSize.preferredHeight = 56f;
            rowSize.flexibleWidth = 1f;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 15f;
            label.color = new Color(0.92f, 0.9f, 0.84f, 1f);
            label.text = name;
            label.raycastTarget = false;
            var labelSize = labelGo.AddComponent<LayoutElement>();
            labelSize.minHeight = 20f;
            labelSize.preferredHeight = 20f;

            var slider = CreateSlider(go.transform);
            var captured = resourceId;
            slider.onValueChanged.AddListener(value => OnSlider(captured, value));

            return new SliderRow
            {
                ResourceId = resourceId,
                Slider = slider,
                Label = label
            };
        }

        static Slider CreateSlider(Transform parent)
        {
            var root = new GameObject("Slider", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.AddComponent<LayoutElement>().preferredHeight = 22f;

            var background = MakeImage(root.transform, "Background", new Color(0.16f, 0.16f, 0.18f, 1f));
            Stretch(background.rectTransform, 0f, 7f, 0f, -7f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), 0f, 7f, -10f, -7f);

            var fill = MakeImage(fillArea.transform, "Fill", new Color(0.86f, 0.7f, 0.32f, 1f));
            Stretch(fill.rectTransform);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>(), 8f, 0f, -8f, 0f);

            var handle = MakeImage(handleArea.transform, "Handle", new Color(0.92f, 0.9f, 0.84f, 1f));
            var handleRt = handle.rectTransform;
            handleRt.anchorMin = new Vector2(0f, 0f);
            handleRt.anchorMax = new Vector2(0f, 1f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(14f, 0f);
            handleRt.anchoredPosition = Vector2.zero;

            var slider = root.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            return slider;
        }

        static Image MakeImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = WhiteSprite();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        static Sprite _whiteSprite;

        static Sprite WhiteSprite()
        {
            if (_whiteSprite != null)
                return _whiteSprite;
            var texture = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _whiteSprite.name = "PyramidSliderWhite";
            return _whiteSprite;
        }

        static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        void OnSlider(FixedString64Bytes resourceId, float perHour)
        {
            if (suppress)
                return;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.ResourceId != resourceId || row.Slider == null)
                    continue;
                row.Slider.maxValue = ExpandedSoftRange(row.Slider.maxValue, perHour);
                break;
            }

            SimCommands.TryPost(new SetPyramidFeedCommand
            {
                ResourceId = resourceId,
                PerHour = perHour
            });
        }

        static float ExpandedSoftRange(float currentRange, float perHour)
        {
            var range = Mathf.Max(InitialFeedSoftRange, currentRange);
            while (perHour >= range * SoftRangeExpansionThreshold
                && range <= float.MaxValue / SoftRangeExpansionFactor)
            {
                range *= SoftRangeExpansionFactor;
            }

            return range;
        }

        void OnClose()
        {
            selection?.Deselect();
            Hide();
        }

        void EnsureUi()
        {
            if (card == null)
                card = gameObject;
            HideHouseChrome();

            if (title == null)
            {
                var t = new GameObject("Title", typeof(RectTransform));
                t.transform.SetParent(card.transform, false);
                title = t.AddComponent<TextMeshProUGUI>();
                title.fontSize = 22f;
                title.color = Color.white;
            }

            if (subtitle == null)
            {
                var t = new GameObject("Subtitle", typeof(RectTransform));
                t.transform.SetParent(card.transform, false);
                subtitle = t.AddComponent<TextMeshProUGUI>();
                subtitle.fontSize = 16f;
                subtitle.color = new Color(0.8f, 0.85f, 0.9f);
            }

            if (status == null)
            {
                var t = new GameObject("Status", typeof(RectTransform));
                t.transform.SetParent(card.transform, false);
                status = t.AddComponent<TextMeshProUGUI>();
                status.fontSize = 14f;
                status.color = new Color(0.75f, 0.78f, 0.8f);
            }

            if (status != null)
            {
                var rt = status.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 12f);
                rt.sizeDelta = new Vector2(-32f, 44f);
            }

            if (sliderRoot == null)
            {
                var root = new GameObject("Sliders", typeof(RectTransform));
                root.transform.SetParent(card.transform, false);
                sliderRoot = root.transform;
            }

            var sliderRt = sliderRoot.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0f, 0f);
            sliderRt.anchorMax = new Vector2(1f, 1f);
            sliderRt.pivot = new Vector2(0.5f, 0.5f);
            sliderRt.offsetMin = new Vector2(16f, 58f);
            sliderRt.offsetMax = new Vector2(-16f, -78f);

            var layout = sliderRoot.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = sliderRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        void HideHouseChrome()
        {
            if (card == null)
                return;
            for (var i = 0; i < HouseChrome.Length; i++)
            {
                var child = card.transform.Find(HouseChrome[i]);
                if (child != null)
                    child.gameObject.SetActive(false);
            }
        }

        static string FormatStatus(
            DynamicBuffer<PyramidFeedLine> feed,
            DynamicBuffer<ResourceInfo> info,
            DynamicBuffer<EraTributeLine> tribute,
            DynamicBuffer<EraLine> eras,
            int eraIndex)
        {
            var eraName = "Era";
            var loyaltyH = 0f;
            if (eras.IsCreated && eraIndex >= 0 && eraIndex < eras.Length)
            {
                eraName = eras[eraIndex].DisplayName.ToString();
                if (string.IsNullOrEmpty(eraName))
                    eraName = eras[eraIndex].EraId.ToString();
                var per = eras[eraIndex].LoyaltyPerEnergy;
                if (tribute.IsCreated && per > 0.0001f)
                {
                    for (var i = 0; i < feed.Length; i++)
                    {
                        var line = feed[i];
                        if (line.PerHour <= 0.0001f)
                            continue;
                        if (!PyramidFeed.IsTribute(tribute, eraIndex, line.ResourceId))
                            continue;
                        loyaltyH += line.PerHour
                            * PyramidFeed.UnitEnergy(info, tribute, eras, eraIndex, line.ResourceId)
                            * per;
                    }
                }
            }

            return $"{eraName}.  Вера +{loyaltyH:0.##}/ч от дани.";
        }

        static string DisplayName(DynamicBuffer<ResourceInfo> info, FixedString64Bytes resourceId)
        {
            for (var i = 0; i < info.Length; i++)
            {
                if (info[i].ResourceId != resourceId)
                    continue;
                var display = info[i].DisplayName.ToString();
                return string.IsNullOrEmpty(display) ? resourceId.ToString() : display;
            }

            return resourceId.ToString();
        }

        static bool TryFindHq(EntityManager em, int id, out Entity entity)
        {
            entity = Entity.Null;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<Headquarters>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            for (var i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].Id != id)
                    continue;
                entity = entities[i];
                return true;
            }

            return false;
        }
    }
}
