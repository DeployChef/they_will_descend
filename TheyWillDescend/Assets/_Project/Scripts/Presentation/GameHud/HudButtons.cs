using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    static class HudButtons
    {
        /// <summary>Цвет подсветки активной кнопки (текущая скорость времени, пауза).</summary>
        public static readonly Color ActiveColor = new Color(0.45f, 0.85f, 1f, 1f);

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

        /// <summary>
        /// Отметить кнопку активной (текущая скорость, пауза). Активное состояние держится,
        /// пока его не снимают, а не только в момент нажатия.
        /// Если на кнопке есть LayeredButton — он переключает слои (обычный спрайт прячется,
        /// активный появляется), иначе перекрашиваются состояния обычного Button.
        /// </summary>
        public static void SetActive(Button button, bool on)
        {
            if (button == null)
                return;

            if (TryFindLayered(button, out var layered))
            {
                layered.IsActive = on;
                return;
            }

            TintColorBlock(button, on);
        }

        static bool TryFindLayered(Button button, out LayeredButton layered)
        {
            if (button.TryGetComponent(out layered))
                return true;

            layered = button.GetComponentInParent<LayeredButton>();
            if (layered != null)
                return true;

            layered = button.GetComponentInChildren<LayeredButton>(true);
            return layered != null;
        }

        static readonly Dictionary<Button, ColorBlock> OriginalColors = new Dictionary<Button, ColorBlock>();

        static void TintColorBlock(Button button, bool on)
        {
            if (!OriginalColors.TryGetValue(button, out var baseColors))
            {
                baseColors = button.colors;
                OriginalColors[button] = baseColors;
            }

            var colors = button.colors;
            if (on)
            {
                colors.normalColor = baseColors.normalColor * ActiveColor;
                colors.highlightedColor = baseColors.highlightedColor * ActiveColor;
                colors.pressedColor = baseColors.pressedColor * ActiveColor;
                colors.selectedColor = colors.normalColor;
            }
            else
            {
                colors = baseColors;
            }

            if (colors.Equals(button.colors))
                return;

            button.colors = colors;
        }
    }
}
