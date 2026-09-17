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

            [Header("Active (кнопка выбрана)")]
            [Tooltip("Цвет активного состояния. Работает как hover: alpha=0 прячет слой, alpha=1 показывает. " +
                     "Так на активной кнопке обычный спрайт прячется, а активный появляется.")]
            public Color activeColor = Color.white;
            [Tooltip("Множитель масштаба активной кнопки (1 = без изменений)")]
            public float activeScale = 1f;
            [Tooltip("Смещение активной кнопки в локальных единицах")]
            public Vector2 activeOffset = Vector2.zero;

            internal Color NormalColor;
            internal float NormalScale;
            internal Vector2 NormalOffset;
            internal bool AnimatesOffset;
        }

        [SerializeField] Layer[] layers = Array.Empty<Layer>();

        [Header("Active")]
        [SerializeField, Tooltip("Кнопка активна/выбрана (например, текущая скорость времени)")]
        bool active;

        [Header("Transition")]
        [SerializeField, Min(0.01f)] float transitionSpeed = 12f;

        [Header("Click")]
        [SerializeField] Button button;
        [SerializeField] UnityEvent onClick;

        enum State { Normal, Hover, Pressed, Selected, Active }

        bool _pointerInside;
        bool _pointerDown;

        /// <summary>
        /// Кнопка активна (выбрана). Слои переходят в <see cref="State.Active"/> и держат его,
        /// пока состояние не снимут: обычный спрайт прячется, активный появляется.
        /// </summary>
        public bool IsActive
        {
            get => active;
            set => active = value;
        }

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
            ApplyInstant(ResolveState());
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
            var interactable = IsInteractable();

            if (_pointerDown && _pointerInside && interactable)
                return State.Pressed;

            // Выбранная кнопка держит активный спрайт, пока её не переключат:
            // ни наведение курсора, ни временная блокировка кнопок не должны его сбрасывать.
            if (active)
                return State.Active;

            if (!interactable)
                return State.Normal;

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
                // Смещение пишем только если оно реально задано — иначе LayeredButton
                // воюет с LayoutGroup, который расставляет такие кнопки сам.
                layer.AnimatesOffset = layer.hoverOffset != Vector2.zero
                    || layer.pressedOffset != Vector2.zero
                    || layer.activeOffset != Vector2.zero;
            }
        }

        void LerpLayer(Layer layer, State target, float t)
        {
            if (layer == null || layer.graphic == null)
                return;

            Color targetColor;
            float targetScale;
            Vector2 targetOffset;

            switch (target)
            {
                case State.Pressed:
                    // Нажатие по активной кнопке не должно показывать обычный спрайт:
                    // оставляем активный цвет, но pressedScale/pressedOffset работают.
                    targetColor = active ? layer.activeColor * layer.pressedColor : layer.pressedColor;
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
                case State.Active:
                    targetColor = layer.activeColor;
                    targetScale = layer.NormalScale * layer.activeScale;
                    targetOffset = layer.NormalOffset + layer.activeOffset;
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
            if (layer.AnimatesOffset)
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

        /// <summary>
        /// Копия настроек hover в active для всех слоёв: активная кнопка выглядит так же,
        /// как при наведении (обычный спрайт спрятан, активный показан).
        /// </summary>
        [ContextMenu("Copy Hover To Active")]
        public void CopyHoverToActive()
        {
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null)
                    continue;

                layer.activeColor = layer.hoverColor;
                layer.activeScale = layer.hoverScale;
                layer.activeOffset = layer.hoverOffset;
            }
        }
    }
}
