using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Authored tooltip panel. Lives on the ribbon, outside the tape mask.
    /// Callers fill text; this follows the pointer in parent space.
    /// </summary>
    public sealed class HudTooltip : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI want;
        [SerializeField] TextMeshProUGUI body;

        RectTransform _rt;
        RectTransform _parent;
        Canvas _canvas;
        bool _visible;

        void Awake()
        {
            _rt = transform as RectTransform;
            _parent = _rt != null ? _rt.parent as RectTransform : null;
            _canvas = GetComponentInParent<Canvas>(true);
        }

        void Start()
        {
            if (!_visible)
                Hide();
        }

        void LateUpdate()
        {
            if (_visible)
                FollowPointer();
        }

        public void Show(string titleText, string wantText, string bodyText)
        {
            if (title != null)
                title.text = titleText ?? string.Empty;
            if (want != null)
                want.text = wantText ?? string.Empty;
            if (body != null)
                body.text = bodyText ?? string.Empty;

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            _visible = true;
            if (_rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);
            FollowPointer();
        }

        public void Hide()
        {
            _visible = false;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        void FollowPointer()
        {
            if (_rt == null)
                return;
            if (_parent == null)
                _parent = _rt.parent as RectTransform;
            if (_parent == null)
                return;
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();

            var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;
            var screen = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, screen, cam, out var local))
                return;
            _rt.anchoredPosition = local + new Vector2(14f, -10f);
        }
    }
}
