using System;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.Agents;
using TheyWillDescend.Presentation.Cameras;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Presentation.GameHud;
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
        [SerializeField, Tooltip("Камера меню паузы. Если пусто — берётся PauseCameraSwitch.Current.")]
        PauseCameraSwitch pauseCamera;

        public static PauseMenuScreen Current { get; private set; }

        public event Action ContinueClicked;
        public event Action SaveClicked;
        public event Action LoadClicked;
        public event Action MainMenuClicked;
        public event Action ToggleRequested;

        public bool IsOpen => gameObject.activeSelf;

        PauseCameraSwitch CameraSwitch => pauseCamera != null ? pauseCamera : PauseCameraSwitch.Current;

        void Awake()
        {
            Current = this;
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

        public void Show()
        {
            gameObject.SetActive(true);
            CameraSwitch?.Engage();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            CameraSwitch?.Release();
        }

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
    }
}
