using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// Start-game panel on MainMenu. Clicks choose the run and transition the app flow.
    /// Does not know about the splash.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] Button startGameButton;
        [SerializeField] Button startDebugButton;
        [SerializeField] Button loadButton;

        void Awake()
        {
            if (startGameButton != null)
                startGameButton.onClick.AddListener(StartGame);
            if (startDebugButton != null)
                startDebugButton.onClick.AddListener(StartDebug);
            if (loadButton != null)
                loadButton.onClick.AddListener(LoadGame);
        }

        void OnEnable()
        {
            if (loadButton != null)
                loadButton.interactable = RunSnapshotStore.HasSlot;
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        void StartGame() => BeginRun(RunKind.Normal, loadSlot: false);

        void StartDebug()
        {
            GameLog.Info("Main menu: Start Debug.");
            BeginRun(RunKind.Debug, loadSlot: false);
        }

        void LoadGame()
        {
            if (!RunSnapshotStore.HasSlot)
            {
                GameLog.Warning("Main menu: no save slot.");
                if (loadButton != null)
                    loadButton.interactable = false;
                return;
            }

            GameLog.Info("Main menu: Load slot.");
            BeginRun(default, loadSlot: true);
        }

        static void BeginRun(RunKind kind, bool loadSlot)
        {
            var session = GameSession.Active;
            if (session == null || session.Flow == null)
            {
                GameLog.Error("MainMenuScreen: app flow is not installed on GameSession.");
                return;
            }

            if (loadSlot)
                session.SetLoadSlot();
            else
                session.SetRunKind(kind);

            session.Flow.TransitionTo(AppStateId.LoadingGame);
        }
    }
}
