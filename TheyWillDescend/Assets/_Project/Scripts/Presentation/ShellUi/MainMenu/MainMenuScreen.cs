using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Shell;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// Start-game panel on MainMenu. A click asks <see cref="ShellService"/> to launch.
    /// Does not know about the splash.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] Button startGameButton;
        [SerializeField] Button startDebugButton;
        [SerializeField] Button loadButton;

        AppContext _context;
        ShellService _shell;

        [Inject]
        public void Construct(AppContext context, ShellService shell)
        {
            _context = context;
            _shell = shell;
        }

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

        void BeginRun(RunKind kind, bool loadSlot)
        {
            if (_context == null || _shell == null)
            {
                GameLog.Error("MainMenuScreen: the menu scope did not inject the shell.");
                return;
            }

            _context.RequestLaunch(loadSlot ? RunLaunch.Slot : new RunLaunch(kind, false));
            _shell.EnterGame();
        }
    }
}
