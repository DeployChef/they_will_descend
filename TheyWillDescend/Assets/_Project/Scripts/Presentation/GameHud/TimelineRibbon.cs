using System.Collections.Generic;
using System.Text;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Gods;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Scrolling era tape. Caret is fixed (left of center); catalog slides.
    /// Mask is on Viewport only. Tooltip sits on this root, above the mask.
    /// </summary>
    public sealed class TimelineRibbon : MonoBehaviour
    {
        [SerializeField] RectTransform viewport;
        [SerializeField] RectTransform tape;
        [SerializeField] RectTransform marksRoot;
        [SerializeField] RectTransform caret;
        [SerializeField] Image fadeLeft;
        [SerializeField] Image fadeRight;
        [SerializeField] HudTooltip tooltip;
        [SerializeField] [Min(1f)] float pixelsPerHour = 10f;
        [SerializeField] [Range(0.15f, 0.45f)] float caretNormalizedX = 0.28f;

        readonly List<Mark> _marks = new();
        readonly List<SegmentView> _segments = new();
        readonly List<Image> _pips = new();
        int _eraSignature = int.MinValue;
        int _markSignature;
        int _tipEra = -1;
        Sprite _white;
        Sprite _fadeLeftSprite;
        Sprite _fadeRightSprite;

        struct Mark
        {
            public float Hours;
            public Color Color;
        }

        struct SegmentView
        {
            public Image Fill;
            public TextMeshProUGUI Label;
        }

        static readonly Color[] EraPalette =
        {
            new(0.42f, 0.36f, 0.22f, 0.92f),
            new(0.38f, 0.28f, 0.22f, 0.92f),
            new(0.36f, 0.18f, 0.16f, 0.92f),
            new(0.22f, 0.34f, 0.32f, 0.92f),
            new(0.40f, 0.38f, 0.28f, 0.92f)
        };

        public void ClearMarks()
        {
            _marks.Clear();
            _markSignature++;
        }

        public void AddMark(float hoursFromStart, Color color)
        {
            _marks.Add(new Mark { Hours = hoursFromStart, Color = color });
            _markSignature++;
        }

        public void AddMarkAt(int day, float hourOfDay, Color color)
        {
            AddMark(day * 24f + hourOfDay, color);
        }

        void Awake() => EnsureChassis();

        void OnEnable() => EnsureChassis();

        void OnDestroy()
        {
            DestroySprite(_fadeLeftSprite);
            DestroySprite(_fadeRightSprite);
        }

        void LateUpdate()
        {
            if (tape == null || viewport == null || caret == null)
                return;
            if (!SimWorld.TryGet(out var em, out var bag)
                || !em.HasComponent<GameTime>(bag)
                || !em.HasBuffer<EraLine>(bag))
                return;

            var time = em.GetComponentData<GameTime>(bag);
            var eras = em.GetBuffer<EraLine>(bag);
            var hour = em.HasComponent<PyramidConfig>(bag)
                ? em.GetComponentData<PyramidConfig>(bag).EraChangeHour
                : 8f;
            var now = EraClock.NowHours(time);
            RebuildErasIfNeeded(eras, hour, time.DayDuration);
            RebuildPipsIfNeeded();
            Scroll(now);
            if (_tipEra >= 0)
                RefreshTip();
        }

        void Scroll(float nowHours)
        {
            var width = viewport.rect.width;
            if (width < 1f)
                return;
            var caretX = width * caretNormalizedX;
            caret.anchorMin = new Vector2(caretNormalizedX, 0f);
            caret.anchorMax = new Vector2(caretNormalizedX, 1f);
            caret.anchoredPosition = Vector2.zero;
            caret.sizeDelta = new Vector2(2f, 8f);
            tape.anchoredPosition = new Vector2(caretX - nowHours * pixelsPerHour, 0f);
        }

        void RebuildErasIfNeeded(DynamicBuffer<EraLine> eras, float eraChangeHour, float dayDuration)
        {
            var signature = eras.Length;
            if (eras.Length > 0)
                signature ^= eras[0].EraId.GetHashCode() * 397;
            if (signature == _eraSignature && _segments.Count == eras.Length)
                return;
            _eraSignature = signature;

            for (var i = tape.childCount - 1; i >= 0; i--)
            {
                var child = tape.GetChild(i);
                if (marksRoot != null && child == marksRoot)
                    continue;
                Destroy(child.gameObject);
            }

            _segments.Clear();
            var end = 0f;
            for (var i = 0; i < eras.Length; i++)
            {
                var start = EraClock.BoundaryHours(eras, eraChangeHour, dayDuration, i);
                end = EraClock.EraEndHours(eras, eraChangeHour, dayDuration, i);
                var duration = end - start;
                if (duration < 0.01f)
                    duration = 1f;
                _segments.Add(BuildSegment(i, eras[i], start, duration));
            }

            tape.sizeDelta = new Vector2(end * pixelsPerHour + 8f, 0f);
        }

        SegmentView BuildSegment(int index, in EraLine era, float startHours, float durationHours)
        {
            var go = new GameObject($"Era_{index}", typeof(RectTransform));
            go.transform.SetParent(tape, false);
            if (marksRoot != null)
                go.transform.SetSiblingIndex(Mathf.Max(0, marksRoot.GetSiblingIndex()));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.12f);
            rt.anchorMax = new Vector2(0f, 0.88f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(startHours * pixelsPerHour, 0f);
            rt.sizeDelta = new Vector2(durationHours * pixelsPerHour - 2f, 0f);

            var fill = go.AddComponent<Image>();
            fill.sprite = WhiteSprite();
            fill.color = EraPalette[index % EraPalette.Length];
            fill.raycastTarget = true;
            var hover = go.AddComponent<EraSegmentHover>();
            hover.Bind(this, index);

            var edge = new GameObject("StartTick", typeof(RectTransform));
            edge.transform.SetParent(go.transform, false);
            var edgeRt = edge.GetComponent<RectTransform>();
            edgeRt.anchorMin = new Vector2(0f, 0f);
            edgeRt.anchorMax = new Vector2(0f, 1f);
            edgeRt.pivot = new Vector2(0.5f, 0.5f);
            edgeRt.sizeDelta = new Vector2(2f, 4f);
            var edgeImage = edge.AddComponent<Image>();
            edgeImage.sprite = WhiteSprite();
            edgeImage.color = new Color(0.92f, 0.9f, 0.84f, 0.85f);
            edgeImage.raycastTarget = false;

            var labelGo = new GameObject("Name", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(8f, 0f);
            labelRt.offsetMax = new Vector2(-4f, 0f);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            var name = era.DisplayName.ToString();
            if (string.IsNullOrEmpty(name))
                name = era.EraId.ToString();
            label.text = name;
            label.fontSize = 13f;
            label.color = new Color(0.92f, 0.9f, 0.84f, 0.95f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            return new SegmentView { Fill = fill, Label = label };
        }

        void RebuildPipsIfNeeded()
        {
            if (marksRoot == null)
                return;
            if (_pips.Count == _marks.Count && _markSignature == _lastPipSignature)
                return;
            _lastPipSignature = _markSignature;

            for (var i = marksRoot.childCount - 1; i >= 0; i--)
                Destroy(marksRoot.GetChild(i).gameObject);
            _pips.Clear();

            for (var i = 0; i < _marks.Count; i++)
            {
                var mark = _marks[i];
                var go = new GameObject($"Mark_{i}", typeof(RectTransform));
                go.transform.SetParent(marksRoot, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(8f, 8f);
                rt.anchoredPosition = new Vector2(mark.Hours * pixelsPerHour, 10f);
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var image = go.AddComponent<Image>();
                image.sprite = WhiteSprite();
                image.color = mark.Color;
                image.raycastTarget = false;
                _pips.Add(image);
            }
        }

        int _lastPipSignature = int.MinValue;

        public void EnsureChassis()
        {
            var group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            if (fadeLeft != null && fadeRight != null)
                ApplyFades();
        }

        public void ShowEraTip(int eraIndex)
        {
            _tipEra = eraIndex;
            RefreshTip();
        }

        public void HideEraTip()
        {
            _tipEra = -1;
            tooltip?.Hide();
        }

        void RefreshTip()
        {
            if (_tipEra < 0)
            {
                tooltip?.Hide();
                return;
            }

            if (!TryFormatTip(_tipEra, out var title, out var want, out var body))
            {
                HideEraTip();
                return;
            }

            tooltip?.Show(title, want, body);
        }

        static bool TryFormatTip(int eraIndex, out string title, out string want, out string body)
        {
            title = string.Empty;
            want = string.Empty;
            body = string.Empty;
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasBuffer<EraLine>(bag))
                return false;
            var eras = em.GetBuffer<EraLine>(bag);
            if (eraIndex < 0 || eraIndex >= eras.Length)
                return false;

            var era = eras[eraIndex];
            title = era.DisplayName.ToString();
            if (string.IsNullOrEmpty(title))
                title = era.EraId.ToString();

            want = FormatWant(em, bag, eraIndex);
            var summary = era.Summary.ToString();
            var cap = $"Потолок веры: {era.MaxLoyalty:0}%";
            body = string.IsNullOrWhiteSpace(summary) ? cap : $"{summary}\n{cap}";
            return true;
        }

        static string FormatWant(EntityManager em, Entity bag, int eraIndex)
        {
            if (!em.HasBuffer<EraTributeLine>(bag))
                return "Боги хотят: ничего";

            var tribute = em.GetBuffer<EraTributeLine>(bag);
            var info = em.HasBuffer<ResourceInfo>(bag) ? em.GetBuffer<ResourceInfo>(bag) : default;
            var names = new StringBuilder();
            for (var i = 0; i < tribute.Length; i++)
            {
                if (tribute[i].EraIndex != eraIndex)
                    continue;
                if (names.Length > 0)
                    names.Append(", ");
                names.Append(ResourceLabel(info, tribute[i].ResourceId));
            }

            return names.Length == 0 ? "Боги хотят: ничего" : $"Боги хотят: {names}";
        }

        static string ResourceLabel(DynamicBuffer<ResourceInfo> info, FixedString64Bytes resourceId)
        {
            if (info.IsCreated)
            {
                for (var i = 0; i < info.Length; i++)
                {
                    if (info[i].ResourceId != resourceId)
                        continue;
                    var display = info[i].DisplayName.ToString();
                    if (!string.IsNullOrEmpty(display))
                        return display;
                    break;
                }
            }

            return resourceId.ToString();
        }

        public sealed class EraSegmentHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            TimelineRibbon _ribbon;
            int _eraIndex;

            public void Bind(TimelineRibbon ribbon, int eraIndex)
            {
                _ribbon = ribbon;
                _eraIndex = eraIndex;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                _ribbon?.ShowEraTip(_eraIndex);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                _ribbon?.HideEraTip();
            }
        }

        void ApplyFades()
        {
            if (_fadeLeftSprite == null)
                _fadeLeftSprite = MakeFadeSprite(true);
            if (_fadeRightSprite == null)
                _fadeRightSprite = MakeFadeSprite(false);

            fadeLeft.sprite = _fadeLeftSprite;
            fadeLeft.type = Image.Type.Simple;
            fadeLeft.color = Color.white;
            fadeLeft.raycastTarget = false;
            var leftRt = fadeLeft.rectTransform;
            leftRt.anchorMin = new Vector2(0f, 0f);
            leftRt.anchorMax = new Vector2(0f, 1f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.sizeDelta = new Vector2(72f, 0f);
            leftRt.anchoredPosition = Vector2.zero;

            fadeRight.sprite = _fadeRightSprite;
            fadeRight.type = Image.Type.Simple;
            fadeRight.color = Color.white;
            fadeRight.raycastTarget = false;
            var rightRt = fadeRight.rectTransform;
            rightRt.anchorMin = new Vector2(1f, 0f);
            rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.sizeDelta = new Vector2(72f, 0f);
            rightRt.anchoredPosition = Vector2.zero;
        }

        Sprite WhiteSprite()
        {
            if (_white != null)
                return _white;
            var texture = Texture2D.whiteTexture;
            _white = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _white.name = "TimelineRibbonWhite";
            return _white;
        }

        static Sprite MakeFadeSprite(bool fadeToLeft)
        {
            const int width = 64;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (var x = 0; x < width; x++)
            {
                var t = x / (float)(width - 1);
                var a = fadeToLeft ? 1f - t : t;
                a = Mathf.SmoothStep(0f, 1f, a);
                texture.SetPixel(x, 0, new Color(0.06f, 0.07f, 0.08f, a));
            }

            texture.Apply();
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, 1f),
                new Vector2(fadeToLeft ? 0f : 1f, 0.5f),
                100f);
            sprite.name = fadeToLeft ? "TimelineFadeLeft" : "TimelineFadeRight";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
                return;
            var texture = sprite.texture;
            if (texture != null && texture != Texture2D.whiteTexture)
            {
                if (Application.isPlaying)
                    Destroy(texture);
                else
                    DestroyImmediate(texture);
            }

            if (Application.isPlaying)
                Destroy(sprite);
            else
                DestroyImmediate(sprite);
        }
    }
}
