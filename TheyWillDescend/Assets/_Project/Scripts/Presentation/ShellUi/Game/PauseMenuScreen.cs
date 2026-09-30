using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.App;
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

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// In-game pause overlay on Game. Not an AppState — Playing stays current.
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
        CancellationTokenSource _loadCts;
        bool _busy;

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

        /// <summary>Playing turns the game map on and hands Esc to this overlay.</summary>
        public void Use(GameInput input)
        {
            if (_input != null)
                _input.PausePressed -= OnPausePressed;
            _input = input;
            if (_input != null)
                _input.PausePressed += OnPausePressed;
        }

        public void Release()
        {
            if (_input != null)
                _input.PausePressed -= OnPausePressed;
            _input = null;
            CancelLoad();
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
            CloseBuildIfBusy();
            RunSnapshotStore.Write(RunSessionSnapshot.Capture());
        }

        void Load()
        {
            if (_busy)
                return;
            if (!RunSnapshotStore.TryRead(out var snapshot))
                return;

            var session = GameSession.Active;
            if (session == null)
                return;

            _busy = true;
            CloseBuildIfBusy();
            Hide();
            _input?.Disable();
            CancelLoad();
            _loadCts = new CancellationTokenSource();
            LoadSlot(session, snapshot, _loadCts.Token).Forget();
        }

        void LeaveToMainMenu()
        {
            if (_busy)
                return;
            var session = GameSession.Active;
            if (session == null || session.Flow == null)
                return;

            _busy = true;
            Hide();
            session.Flow.TransitionTo(AppStateId.ReturningToMenu);
        }

        async UniTaskVoid LoadSlot(GameSession session, RunSnapshot snapshot, CancellationToken cancellationToken)
        {
            var ready = false;
            try
            {
                await session.RunWithLoadingAsync(
                    async ct =>
                    {
                        if (!RunSessionSnapshot.BeginApply(snapshot, session.TechCatalogs))
                            return;
                        if (!await session.WaitForPhaseAsync(SimSessionPhase.Ready, ct))
                            return;

                        if (!SimCommands.TryPost(SimClockCommand.InGame(true)))
                            return;
                        RebuildViews();
                        ready = true;
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _busy = false;
                var flow = session.Flow;
                if (ready && flow != null && flow.CurrentId == AppStateId.Playing)
                    _input?.EnableGame();
                else if (!cancellationToken.IsCancellationRequested)
                {
                    GameLog.Error("Playing load failed — ECS did not reach Ready; input remains disabled.");
                    if (flow != null && flow.CurrentId == AppStateId.Playing)
                        flow.TransitionTo(AppStateId.ReturningToMenu);
                }
            }
        }

        void CancelLoad()
        {
            if (_loadCts == null)
                return;
            _loadCts.Cancel();
            _loadCts.Dispose();
            _loadCts = null;
        }
    }
}
