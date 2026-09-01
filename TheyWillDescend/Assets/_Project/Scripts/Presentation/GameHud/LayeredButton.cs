using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Кнопка из нескольких слоёв (background / frame / icon).
    /// Каждый слой настраивается отдельно: цвет, масштаб и смещение для hover и pressed.
    /// Вешается на корень кнопки (рядом с Button, либо вместо неё — есть UnityEvent onClick).
    /// </summary>
    public sealed class LayeredButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [Serializable]
        public sealed class Layer
        {
            [Tooltip("Графика слоя: Image на дочернем объекте")]
            public Graphic graphic;

            [Header("Hover")]
            public Color hoverColor = Color.white;
            [Tooltip("Множитель масштаба при наведении (1 = без изменений)")]
            public float hoverScale = 1f;
            [Tooltip("Смещение при наведении в локальных единицах")]
            public Vector2 hoverOffset = Vector2.zero;

            [Header("Pressed")]
            public Color pressedColor = Color.white;
            public float pressedScale = 1f;
            public Vector2 pressedOffset = Vector2.zero;

            [Header("Selected (клавиатура/геймпад)")]
            public bool useSelectedState;
            public Color selectedColor = Color.white;
            public float selectedScale = 1f;

            internal Color NormalColor;
            internal float NormalScale;
            internal Vector2 NormalOffset;
        }

        [SerializeField] Layer[] layers = Array.Empty<Layer>();

        [Header("Transition")]
        [SerializeField, Min(0.01f)] float transitionSpeed = 12f;

        [Header("Click")]
        [SerializeField] Button button;
        [SerializeField] UnityEvent onClick;

        enum State { Normal, Hover, Pressed, Selected }

        bool _pointerInside;
        bool _pointerDown;

        void Awake()
        {
            CaptureNormalState();

            if (button == null)
                button = GetComponent<Button>();
        }

        void OnEnable()
        {
            _pointerInside = false;
            _pointerDown = false;
            ApplyInstant(State.Normal);
        }

        void Update()
        {
            var target = ResolveState();
            var t = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
            for (var i = 0; i < layers.Length; i++)
                LerpLayer(layers[i], target, t);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            _pointerDown = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDown = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pointerDown = false;
            if (_pointerInside && IsInteractable())
                onClick?.Invoke();
        }

        State ResolveState()
        {
            if (!IsInteractable())
                return State.Normal;

            if (_pointerDown && _pointerInside)
                return State.Pressed;
            if (_pointerInside)
                return State.Hover;

            var selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            if (selected)
                return State.Selected;

            return State.Normal;
        }

        bool IsInteractable()
        {
            return button == null || button.interactable;
        }

        void CaptureNormalState()
        {
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null || layer.graphic == null)
                    continue;

                layer.NormalColor = layer.graphic.color;
                layer.NormalScale = layer.graphic.rectTransform.localScale.x;
                layer.NormalOffset = layer.graphic.rectTransform.anchoredPosition;
            }
        }

        static void LerpLayer(Layer layer, State target, float t)
        {
            if (layer == null || layer.graphic == null)
                return;

            Color targetColor;
            float targetScale;
            Vector2 targetOffset;

            switch (target)
            {
                case State.Pressed:
                    targetColor = layer.pressedColor;
                    targetScale = layer.NormalScale * layer.pressedScale;
                    targetOffset = layer.NormalOffset + layer.pressedOffset;
                    break;
                case State.Hover:
                    targetColor = layer.hoverColor;
                    targetScale = layer.NormalScale * layer.hoverScale;
                    targetOffset = layer.NormalOffset + layer.hoverOffset;
                    break;
                case State.Selected:
                    targetColor = layer.useSelectedState ? layer.selectedColor : layer.NormalColor;
                    targetScale = layer.NormalScale * (layer.useSelectedState ? layer.selectedScale : 1f);
                    targetOffset = layer.NormalOffset;
                    break;
                default:
                    targetColor = layer.NormalColor;
                    targetScale = layer.NormalScale;
                    targetOffset = layer.NormalOffset;
                    break;
            }

            var rt = layer.graphic.rectTransform;
            layer.graphic.color = Color.Lerp(layer.graphic.color, targetColor, t);
            var scale = Mathf.Lerp(rt.localScale.x, targetScale, t);
            rt.localScale = new Vector3(scale, scale, rt.localScale.z);
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetOffset, t);
        }

        void ApplyInstant(State target)
        {
            for (var i = 0; i < layers.Length; i++)
                LerpLayer(layers[i], target, 1f);
        }

        /// <summary>Перечитать нормальное состояние слоёв (после смены спрайтов в редакторе).</summary>
        [ContextMenu("Capture Normal State")]
        public void CaptureNormal()
        {
            CaptureNormalState();
            ApplyInstant(State.Normal);
        }
    }
}
