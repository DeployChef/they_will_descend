using TheyWillDescend.Simulation.Gods;
using TheyWillDescend.Simulation.Session;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Bottom gods-loyalty bar. Pulls GodLoyalty. Always 0–100; red is the forbidden cap zone.
    /// Display-only: does not eat world clicks.
    /// </summary>
    public sealed class FaithWidget : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Image fill;
        [SerializeField] Image forbidden;

        static readonly Color Track = new(0.12f, 0.12f, 0.14f, 0.95f);
        static readonly Color FillGold = new(0.86f, 0.7f, 0.32f, 1f);
        static readonly Color ForbiddenRed = new(0.62f, 0.16f, 0.14f, 0.92f);
        static readonly Color Panel = new(0.07f, 0.08f, 0.1f, 0.78f);
        static readonly Color Ink = new(0.92f, 0.9f, 0.84f, 1f);

        void Awake()
        {
            if (fill == null || label == null)
                BuildFallback();
            ApplyLook();
        }

        void OnEnable() => DisableRaycasts();

        void Update()
        {
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasComponent<GodLoyalty>(bag))
                return;

            var loyalty = em.GetComponentData<GodLoyalty>(bag);
            var value = Mathf.Clamp(loyalty.Value, 0f, 100f);
            var max = Mathf.Clamp(loyalty.EffectiveMax, 0f, 100f);
            if (label != null)
                label.text = $"Вера  {value:0}%";
            if (fill != null)
                fill.fillAmount = value / 100f;
            if (forbidden != null)
                forbidden.fillAmount = (100f - max) / 100f;
        }

        void ApplyLook()
        {
            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 14f);
                rt.sizeDelta = new Vector2(280f, 28f);
            }

            var bg = GetComponent<Image>();
            if (bg != null)
                bg.color = Panel;

            if (label != null)
            {
                var lrt = label.rectTransform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(0.38f, 1f);
                lrt.offsetMin = new Vector2(10f, 0f);
                lrt.offsetMax = new Vector2(-4f, 0f);
                label.fontSize = 14f;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.color = Ink;
                label.text = "Вера";
            }

            var bar = fill != null ? fill.transform.parent as RectTransform : null;
            if (bar != null)
            {
                bar.anchorMin = new Vector2(0.38f, 0.32f);
                bar.anchorMax = new Vector2(0.97f, 0.68f);
                bar.offsetMin = Vector2.zero;
                bar.offsetMax = Vector2.zero;
                var track = bar.GetComponent<Image>();
                if (track != null)
                    track.color = Track;
            }

            if (fill != null)
            {
                fill.color = FillGold;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            }

            if (forbidden != null)
            {
                forbidden.color = ForbiddenRed;
                forbidden.type = Image.Type.Filled;
                forbidden.fillMethod = Image.FillMethod.Horizontal;
                forbidden.fillOrigin = (int)Image.OriginHorizontal.Right;
            }

            DisableRaycasts();
        }

        void DisableRaycasts()
        {
            var group = GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var graphics = GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        void BuildFallback()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null)
                rt = gameObject.AddComponent<RectTransform>();

            if (gameObject.GetComponent<Image>() == null)
                gameObject.AddComponent<Image>();

            var bar = new GameObject("Bar", typeof(RectTransform));
            bar.transform.SetParent(transform, false);
            bar.AddComponent<Image>();

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(bar.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fill = fillGo.AddComponent<Image>();

            var redGo = new GameObject("Forbidden", typeof(RectTransform));
            redGo.transform.SetParent(bar.transform, false);
            var redRt = redGo.GetComponent<RectTransform>();
            redRt.anchorMin = Vector2.zero;
            redRt.anchorMax = Vector2.one;
            redRt.offsetMin = Vector2.zero;
            redRt.offsetMax = Vector2.zero;
            forbidden = redGo.AddComponent<Image>();

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(transform, false);
            label = labelGo.AddComponent<TextMeshProUGUI>();
        }
    }
}
