using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    static class HudButtons
    {
        public static void Bind(Button button, UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        public static void Unbind(Button button, UnityAction action)
        {
            if (button != null)
                button.onClick.RemoveListener(action);
        }

        public static void SetInteractable(Button button, bool value)
        {
            if (button != null)
                button.interactable = value;
        }

        public static void SetLabel(Button button, string text)
        {
            if (button == null)
                return;
            var tmp = button.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (tmp != null)
                tmp.text = text;
        }

        public static void Tint(Button button, bool on)
        {
            if (button == null)
                return;
            var colors = button.colors;
            colors.normalColor = on ? new Color(0.35f, 0.7f, 1f, 1f) : Color.white;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
        }
    }
}
