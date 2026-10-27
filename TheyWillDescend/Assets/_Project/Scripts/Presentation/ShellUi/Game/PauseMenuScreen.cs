using System.Collections;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Presentation.Agents;
using TheyWillDescend.Presentation.Cameras;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Presentation.GameHud;
using TheyWillDescend.Shell;
using TheyWillDescend.Simulation.Session;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// In-game pause overlay on Game. Esc does not change scenes.
    /// Открытие/закрытие проигрывается последовательностью элементов, синхронизированной с камерой.
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
        [SerializeField, Tooltip("Последовательность появления элементов меню. Пусто — меню показывается целиком сразу.")]
        UiSequencePlayer revealSequence;
        [SerializeField, Range(0f, 1f), Tooltip("Доля прохода камеры до меню, после которой начинается появление элементов.")]
        float revealAtBlendProgress = 0.9f;
        [SerializeField, Min(0f), Tooltip("Запас секунд к длительности перехода камеры на случай, если порог так и не наступил.")]
        float revealFallbackWait = 0.25f;

        public static PauseMenuScreen Current { get; private set; }

        /// <summary>Меню открыто логически. На время исчезновения элементов сбрасывается сразу.</summary>
        public bool IsOpen { get; private set; }

        GameInput _input;
        ShellService _shell;
        SaveService _save;
        bool _busy;

        [Inject]
        public void Construct(ShellService shell, GameInput input, SaveService save)
        {
            _shell = shell;
            _input = input;
            _save = save;
        }

        PauseCameraSwitch CameraSwitch => pauseCamera != null ? pauseCamera : PauseCameraSwitch.Current;

        float CameraBlendProgress => CameraSwitch != null ? CameraSwitch.MenuBlendProgress : 1f;

        Coroutine _revealRoutine;

        void Awake()
        {
            Current = this;
            Bind(continueButton, Continue);
            Bind(saveButton, Save);
            Bind(loadButton, Load);
            Bind(mainMenuButton, LeaveToMainMenu);
            HideImmediate();
        }

        void OnDestroy()
        {
            Release();
            if (Current == this)
                Current = null;
        }

        /// <summary><see cref="GameEntry"/> turns the game map on. Esc is the injected <see cref="GameInput"/>.</summary>
        public void Use()
        {
            if (_input == null)
                return;
            _input.PausePressed -= OnPausePressed;
            _input.PausePressed += OnPausePressed;
        }

        public void Release()
        {
            if (_input != null)
                _input.PausePressed -= OnPausePressed;
            _busy = false;
            HideImmediate();
        }

        public void Show()
        {
            if (IsOpen)
                return;

            IsOpen = true;
            gameObject.SetActive(true);
            CameraSwitch?.Engage();

            if (revealSequence == null)
                return;

            revealSequence.HideImmediate();
            StopReveal();
            _revealRoutine = StartCoroutine(RevealWhenCameraArrives());
        }

        public void Hide()
        {
            if (!IsOpen)
            {
                // Меню уже закрыто: дотушиваем состояние, но не трогаем идущее исчезновение.
                if (revealSequence == null || !revealSequence.IsPlaying)
                {
                    StopReveal();
                    gameObject.SetActive(false);
                }
                return;
            }

            IsOpen = false;
            StopReveal();
            CameraSwitch?.Release();

            if (revealSequence == null)
            {
                gameObject.SetActive(false);
                return;
            }

            revealSequence.PlayOut(() =>
            {
                if (!IsOpen)
                    gameObject.SetActive(false);
            });
        }

        /// <summary>Закрыть без анимации — для старта сцены и ухода из состояния.</summary>
        public void HideImmediate()
        {
            IsOpen = false;
            StopReveal();
            CameraSwitch?.Release();
            if (revealSequence != null)
                revealSequence.HideImmediate();
            gameObject.SetActive(false);
        }

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

        /// <summary>Ждём, пока камера почти доедет до меню, и только тогда раскрываем элементы.</summary>
        IEnumerator RevealWhenCameraArrives()
        {
            // Заезд камеры может быть растянут (орбита унесла позу далеко), поэтому страховочный
            // бюджет считаем от фактической длительности перехода, а не от фиксированного числа.
            float budget = RevealFallbackBudget();
            float waited = 0f;
            while (waited < budget && CameraBlendProgress < revealAtBlendProgress)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            _revealRoutine = null;
            if (!IsOpen || revealSequence == null)
                yield break;

            revealSequence.PlayIn();
        }

        /// <summary>
        /// Страховочный бюджет ожидания: фактическая длительность перехода камеры плюс запас
        /// (или минимум из инспектора, если камера не сообщает длительность).
        /// </summary>
        float RevealFallbackBudget()
        {
            var cameraSwitch = CameraSwitch;
            if (cameraSwitch != null && cameraSwitch.LastBlendDuration > 0f)
                return cameraSwitch.LastBlendDuration + revealFallbackWait;
            return revealFallbackWait;
        }

        void StopReveal()
        {
            if (_revealRoutine == null)
                return;
            StopCoroutine(_revealRoutine);
            _revealRoutine = null;
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        void OnPausePressed()
        {
            if (_busy)
                return;
            if (ResearchWidget.Current != null && ResearchWidget.Current.TryHandleEscape())
                return;
            if (BuildWidget.Current != null && BuildWidget.Current.TryHandleEscape())
                return;

            if (IsOpen)
                Continue();
            else
                Open();
        }

        void Open()
        {
            if (_busy)
                return;
            CloseBuildIfBusy();
            Show();
            SimCommands.TryPost(SimClockCommand.PlayerPaused(true));
        }

        void Continue()
        {
            Hide();
            SimCommands.TryPost(SimClockCommand.PlayerPaused(false));
        }

        void Save()
        {
            if (_busy)
                return;
            if (_save == null)
            {
                GameLog.Error("PauseMenuScreen: SaveService was not injected.");
                return;
            }

            CloseBuildIfBusy();
            _save.SaveCurrent();
        }

        void Load()
        {
            if (_busy)
                return;
            if (_save == null)
            {
                GameLog.Error("PauseMenuScreen: SaveService was not injected.");
                return;
            }

            if (!_save.HasSlot)
                return;
            if (_shell == null)
            {
                GameLog.Error("PauseMenuScreen: ShellService was not injected.");
                return;
            }

            _busy = true;
            CloseBuildIfBusy();
            Hide();
            Navigate(_shell.RestartRun(RunLaunch.Slot)).Forget();
        }

        void LeaveToMainMenu()
        {
            if (_busy)
                return;
            if (_shell == null)
            {
                GameLog.Error("PauseMenuScreen: ShellService was not injected.");
                return;
            }

            _busy = true;
            Navigate(_shell.GoToMenu()).Forget();
        }

        /// <summary>The scene goes away on success; on failure the menu becomes usable again.</summary>
        async UniTaskVoid Navigate(UniTask<bool> transition)
        {
            if (!await transition && this != null)
                _busy = false;
        }
    }
}
