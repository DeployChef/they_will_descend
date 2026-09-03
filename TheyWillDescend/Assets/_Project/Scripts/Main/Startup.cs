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
        [SerializeField] bool skipMenuToGameTemporarily = true;


        [SerializeField] GameAudio gameAudio;
        [SerializeField] GameInput gameInput;
        [SerializeField] GameSession gameSession;

        private AppStateMachine _fsm;
        private bool _started;

        void Awake()
        {
            if (_started)
                return;
            _started = true;
            BootAsync().Forget();
        }

        async UniTaskVoid BootAsync()
        {
            var ct = this.GetCancellationTokenOnDestroy();

            if (gameAudio == null)
            {
                GameLog.Error("Startup: GameAudio must be assigned. Put it on its own Bootstrap object.");
                throw new InvalidOperationException(
                    "Startup is missing GameAudio. Assign the GameAudio object, do not AddComponent from code.");
            }

            if (gameInput == null)
            {
                GameLog.Error("Startup: GameInput must be assigned. Put it on its own Bootstrap object.");
                throw new InvalidOperationException(
                    "Startup is missing GameInput. Assign the GameInput object, do not AddComponent from code.");
            }

            if (gameSession == null)
            {
                GameLog.Error("Startup: GameSession must be assigned. Put it on its own Bootstrap object.");
                throw new InvalidOperationException(
                    "Startup is missing GameSession. Assign the GameSession object, do not AddComponent from code.");
            }

            if (!skipMenuToGameTemporarily)
                await gameSession.LoadMainMenuAsync(ct);

            _fsm = AppFlowFactory.Create(gameSession, gameAudio, gameInput);

            if (skipMenuToGameTemporarily)




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
