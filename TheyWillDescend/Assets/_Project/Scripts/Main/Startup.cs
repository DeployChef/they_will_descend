using System;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Shell;
using UnityEngine;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// Composition root. Lives on Bootstrap. Wires the app: scenes, Shell FSM.
    /// </summary>
    public sealed class Startup : MonoBehaviour
    {
        [Header("Temporary debug")]
        [Tooltip("Skip PressAnyKey/MainMenu and load Game immediately. Turn OFF before shipping flow work.")]
        [SerializeField] bool skipMenuToGameTemporarily = true;

        [SerializeField] GameAudio gameAudio;
        [SerializeField] GameInput gameInput;

        AppStateMachine _fsm;
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

            var audio = gameAudio;
            if (audio == null)
            {
                GameLog.Error("Startup: GameAudio must be assigned. Put it on its own Bootstrap object.");
                throw new InvalidOperationException(
                    "Startup is missing GameAudio. Assign the GameAudio object, do not AddComponent from code.");
            }

            var input = gameInput;
            if (input == null)
            {
                GameLog.Error("Startup: GameInput must be assigned. Put it on its own Bootstrap object.");
                throw new InvalidOperationException(
                    "Startup is missing GameInput. Assign the GameInput object, do not AddComponent from code.");
            }

            var bundle = AppFlowFactory.Create(scenes, audio, input);
            _fsm = bundle.StateMachine;
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
        }
    }
}
