using System;
using System.Collections;
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
        [SerializeField, Min(0f), Tooltip("Сколько секунд ждать камеру, прежде чем показать меню принудительно.")]
        float revealFallbackWait = 1.2f;

        public static PauseMenuScreen Current { get; private set; }

        public event Action ContinueClicked;
        public event Action SaveClicked;
        public event Action LoadClicked;
        public event Action MainMenuClicked;
        public event Action ToggleRequested;

        /// <summary>Меню открыто логически. На время исчезновения элементов сбрасывается сразу.</summary>
        public bool IsOpen { get; private set; }

        PauseCameraSwitch CameraSwitch => pauseCamera != null ? pauseCamera : PauseCameraSwitch.Current;

        float CameraBlendProgress => CameraSwitch != null ? CameraSwitch.MenuBlendProgress : 1f;

        Coroutine _revealRoutine;

        void Awake()
        {
            Current = this;
            Bind(continueButton, () => ContinueClicked?.Invoke());
            Bind(saveButton, () => SaveClicked?.Invoke());
            Bind(loadButton, () => LoadClicked?.Invoke());
            Bind(mainMenuButton, () => MainMenuClicked?.Invoke());
            HideImmediate();
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
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

        /// <summary>Ждём, пока камера почти доедет до меню, и только тогда раскрываем элементы.</summary>
        IEnumerator RevealWhenCameraArrives()
        {
            float waited = 0f;
            while (waited < revealFallbackWait && CameraBlendProgress < revealAtBlendProgress)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            _revealRoutine = null;
            if (!IsOpen || revealSequence == null)
                yield break;

            revealSequence.PlayIn();
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
    }
}
