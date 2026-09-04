using System;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.Agents;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Presentation.GameHud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// In-game pause overlay on Game. Not an AppState — Playing stays current.
    /// </summary>
    public sealed class PauseMenuScreen : MonoBehaviour
    {
        [SerializeField] Button continueButton;
        [SerializeField] Button saveButton;
        [SerializeField] Button loadButton;
        [SerializeField] Button mainMenuButton;
        [SerializeField] BuildWidget buildWidget;
        [SerializeField] BuildingViewBoard buildingViewBoard;
        [SerializeField] AgentViewBoard agentViewBoard;

        public static PauseMenuScreen Current { get; private set; }

        public event Action ContinueClicked;
        public event Action SaveClicked;
        public event Action LoadClicked;
        public event Action MainMenuClicked;
        public event Action ToggleRequested;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            Current = this;
            if (continueButton == null)
                BuildChrome();
            Bind(continueButton, () => ContinueClicked?.Invoke());
            Bind(saveButton, () => SaveClicked?.Invoke());
            Bind(loadButton, () => LoadClicked?.Invoke());
            Bind(mainMenuButton, () => MainMenuClicked?.Invoke());
            Hide();
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        public void RequestToggle() => ToggleRequested?.Invoke();

        public void CloseBuildIfBusy()
        {
            buildWidget?.CloseIfBusy();
            ResearchWidget.Current?.CloseIfBusy();
        }

        public void RebuildViews()
        {
            agentViewBoard?.Pump();
            if (buildingViewBoard == null)
                GameLog.Error("PauseMenuScreen: BuildingViewBoard is not assigned.");
            else
                buildingViewBoard.RebuildViews();
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        void BuildChrome()
        {
            var dim = GetComponent<Image>();
            if (dim == null)
                dim = gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);
            dim.raycastTarget = true;

            var card = new GameObject("MenuCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(transform, false);
            var cardRt = (RectTransform)card.transform;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(320f, 292f);
            card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.96f);

            var layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            continueButton = CreateButton(card.transform, "Continue");
            saveButton = CreateButton(card.transform, "Save");
            loadButton = CreateButton(card.transform, "Load");
            mainMenuButton = CreateButton(card.transform, "Main Menu");
        }

        static Button CreateButton(Transform parent, string label)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.2f, 0.92f);
            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 48f;
            layout.preferredHeight = 48f;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 0.9f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.75f, 0.85f, 1f);
            button.colors = colors;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 22f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            return button;
        }
    }
}
