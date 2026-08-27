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
        [SerializeField] GameSession gameSession;

        AppStateMachine _fsm;
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
            var skipMenu = skipMenuToGameTemporarily;
            var ct = this.GetCancellationTokenOnDestroy();

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

            var session = gameSession;
            if (session == null)
            {
                GameLog.Error("Startup: GameSession must be assigned. Put it on its own Bootstrap object.");
                throw new InvalidOperationException(
                    "Startup is missing GameSession. Assign the GameSession object, do not AddComponent from code.");
            }

            if (!skipMenu)
                await session.LoadMainMenuAsync(ct);

            _fsm = AppFlowFactory.Create(session, audio, input);

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
    }
}
