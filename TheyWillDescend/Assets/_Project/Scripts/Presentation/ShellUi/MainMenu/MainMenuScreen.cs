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

        SaveService _save;
        ShellService _shell;

        [Inject]
        public void Construct(SaveService save, ShellService shell)
        {
            _save = save;
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
                loadButton.interactable = _save != null && _save.HasSlot;
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
            if (_save == null)
            {
                GameLog.Error("MainMenuScreen: SaveService was not injected.");
                return;
            }

            if (!_save.HasSlot)
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
            if (_shell == null)
            {
                GameLog.Error("MainMenuScreen: ShellService was not injected.");
                return;
            }

            _shell.EnterGame(loadSlot ? RunLaunch.Slot : new RunLaunch(kind, false));
        }
    }
}
