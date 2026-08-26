using System;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Shell;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// Composition root. Lives on Bootstrap. Wires the app: scenes, Shell FSM, input asset.
    /// </summary>
    public sealed class Startup : MonoBehaviour
    {
        [Header("Temporary debug")]
        [Tooltip("Skip PressAnyKey/MainMenu and load Game immediately. Turn OFF before shipping flow work.")]
        [SerializeField] bool skipMenuToGameTemporarily = true;

        [Header("Input")]
        [SerializeField] InputActionAsset inputActions;

        AppStateMachine _fsm;
        GameInput _input;
        GameSession _session;
        bool _started;

        void Awake()
        {
            if (_started)
                return;
            _started = true;
            BootAsync().Forget();
        }

        async UniTaskVoid BootAsync()
        {
            var scenes = new SceneLoader();
            var skipMenu = skipMenuToGameTemporarily;
            var ct = this.GetCancellationTokenOnDestroy();

            if (!skipMenu)
                await scenes.LoadMainMenuAdditive(ct);

            var audio = GetComponent<GameAudio>();
            if (audio == null)
            {
                GameLog.Error("Startup: GameAudio must be on Bootstrap. Do not AddComponent it from code.");
                throw new InvalidOperationException(
                    "Bootstrap is missing GameAudio. Add it on the scene, not from Startup.");
            }

            if (inputActions == null)
            {
                GameLog.Error(
                    "Startup: Input Action Asset must be assigned on Bootstrap (TheyWillDescend.inputactions).");
                throw new InvalidOperationException(
                    "Bootstrap is missing Input Action Asset. Assign Assets/_Project/Input/TheyWillDescend.inputactions.");
            }

            var bundle = AppFlowFactory.Create(scenes, audio, inputActions);
            _fsm = bundle.StateMachine;
            _input = bundle.Input;
            _session = bundle.Session;

            if (skipMenu)
            {
                GameLog.Warning("TEMPORARY: skipMenuToGame — starting at LoadingGame (MainMenu not loaded).");
                _fsm.Start(AppStateId.LoadingGame);
            }
            else
            {
                GameLog.Info("Startup ready (Root). AppFlow started.");
                _fsm.Start(AppStateId.PressAnyKey);
            }
        }

        void OnDestroy()
        {
            _session?.Cancel();
            _session = null;
            _input?.Dispose();
            _input = null;
        }
    }
}
