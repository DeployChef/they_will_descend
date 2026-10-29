using Cysharp.Threading.Tasks;
using TheyWillDescend.Presentation.Infrastructure;
using TheyWillDescend.Presentation.Shell;
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
                loadButton.interactable = _save != null && _save.Exists(SaveService.Slot0);
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        void StartGame() => Begin(RunLaunch.New());

        void StartDebug()
        {
            GameLog.Info("Main menu: Start Debug.");
            Begin(RunLaunch.New(debug: true));
        }

        void LoadGame()
        {
            if (_save == null)
            {
                GameLog.Error("MainMenuScreen: SaveService was not injected.");
                return;
            }

            if (!_save.Exists(SaveService.Slot0))
            {
                GameLog.Warning("Main menu: no save slot0.");
                if (loadButton != null)
                    loadButton.interactable = false;
                return;
            }

            GameLog.Info("Main menu: Load slot0.");
            Begin(RunLaunch.FromSave(SaveService.Slot0));
        }

        void Begin(RunLaunch launch)
        {
            if (_shell == null)
            {
                GameLog.Error("MainMenuScreen: ShellService was not injected.");
                return;
            }

            _shell.StartRun(launch).Forget();
        }
    }
}
