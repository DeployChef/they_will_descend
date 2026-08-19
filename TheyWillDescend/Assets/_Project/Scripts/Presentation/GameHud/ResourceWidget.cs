using TheyWillDescend.Simulation.Io;
using TMPro;
using UnityEngine;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Pulls ResourceStock. Does not produce.
    /// </summary>
    public sealed class ResourceWidget : MonoBehaviour
    {
        [SerializeField] TMP_Text resource1Label;
        [SerializeField] TMP_Text resource2Label;
        [SerializeField] TMP_Text resource3Label;
        [SerializeField] TMP_Text resource4Label;

        void Awake()
        {
            EnsureLabels();
        }

        void Update()
        {
            EnsureLabels();
            if (!SimIo.TryGetStock(out var stock))
            {
                Set("R1 --", "R2 --", "R3 --", "R4 --");
                return;
            }

            Set(
                $"R1 {Mathf.FloorToInt(stock.Resource1)}",
                $"R2 {Mathf.FloorToInt(stock.Resource2)}",
                $"R3 {Mathf.FloorToInt(stock.Resource3)}",
                $"R4 {Mathf.FloorToInt(stock.Resource4)}");
        }

        void Set(string a, string b, string c, string d)
        {
            if (resource1Label != null)
                resource1Label.text = a;
            if (resource2Label != null)
                resource2Label.text = b;
            if (resource3Label != null)
                resource3Label.text = c;
            if (resource4Label != null)
                resource4Label.text = d;
        }

        void EnsureLabels()
        {
            if (resource1Label != null)
                return;

            var font = GetComponentInParent<TimeWidget>()?.GetComponentInChildren<TMP_Text>()?.font;
            var row = new GameObject("ResourceRow", typeof(RectTransform));
            row.transform.SetParent(transform, false);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.offsetMin = new Vector2(8f, 0f);
            rowRect.offsetMax = Vector2.zero;
            var layout = row.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;

            resource1Label = CreateLabel(row.transform, "R1", font);
            resource2Label = CreateLabel(row.transform, "R2", font);
            resource3Label = CreateLabel(row.transform, "R3", font);
            resource4Label = CreateLabel(row.transform, "R4", font);
        }

        static TMP_Text CreateLabel(Transform parent, string name, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.color = Color.white;
            if (font != null)
                text.font = font;
            text.text = $"{name} 0";
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(90f, 24f);
            return text;
        }
    }
}
