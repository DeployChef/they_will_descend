using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// Start-game panel on MainMenu. Does not know about the splash.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] Button startGameButton;
        [SerializeField] Button startDebugButton;
        [SerializeField] Button loadButton;

        public static MainMenuScreen Current { get; private set; }

        public event Action StartClicked;
        public event Action DebugClicked;
        public event Action LoadClicked;

        void Awake()
        {
            Current = this;
            if (loadButton == null && startDebugButton != null)
                loadButton = CloneMenuButton(startDebugButton, "LoadButton", "Load");
            LayoutButtons();
            if (startGameButton != null)
                startGameButton.onClick.AddListener(() => StartClicked?.Invoke());
            if (startDebugButton != null)
                startDebugButton.onClick.AddListener(() => DebugClicked?.Invoke());
            if (loadButton != null)
                loadButton.onClick.AddListener(() => LoadClicked?.Invoke());
            Hide();
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        public void SetLoadEnabled(bool enabled)
        {
            if (loadButton != null)
                loadButton.interactable = enabled;
        }

        void LayoutButtons()
        {
            var stack = GetComponentInChildren<VerticalLayoutGroup>(true);
            RectTransform root;
            if (stack == null)
            {
                var go = new GameObject("ButtonStack", typeof(RectTransform));
                root = (RectTransform)go.transform;
                root.SetParent(transform, false);
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = Vector2.zero;
                var layout = go.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 18f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                var fitter = go.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                root = (RectTransform)stack.transform;
            }

            PlaceInStack(startGameButton, root, 0);
            PlaceInStack(loadButton, root, 1);
            PlaceInStack(startDebugButton, root, 2);
        }

        static void PlaceInStack(Button button, Transform stack, int index)
        {
            if (button == null)
                return;

            var rt = (RectTransform)button.transform;
            rt.SetParent(stack, false);
            rt.localScale = Vector3.one;
            rt.SetSiblingIndex(index);
            var element = button.GetComponent<LayoutElement>();
            if (element == null)
                element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 320f;
            element.minWidth = 320f;
            element.preferredHeight = 64f;
            element.minHeight = 64f;
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp == null)
                return;
            tmp.fontSize = 24f;
            tmp.enableAutoSizing = false;
        }

        static Button CloneMenuButton(Button source, string objectName, string label)
        {
            var go = Instantiate(source.gameObject, source.transform.parent);
            go.name = objectName;
            var tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
                tmp.text = label;
            var button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            return button;
        }
    }
}
