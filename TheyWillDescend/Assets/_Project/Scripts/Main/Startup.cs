using System;
using System.Collections;
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

        AppStateMachine _fsm;
        IDisposable _intents;
        bool _started;

        void Awake()
        {
            if (_started)
                return;
            _started = true;
            StartCoroutine(BootRoutine());
        }

        IEnumerator BootRoutine()
        {
            var scenes = new SceneLoader();
            var skipMenu = skipMenuToGameTemporarily;

            if (!skipMenu)
            {
                yield return scenes.LoadMainMenuAdditive();
                yield return null;
            }

            var audio = GetComponent<GameAudio>();
            if (audio == null)
            {
                GameLog.Error("Startup: GameAudio must be on Bootstrap. Do not AddComponent it from code.");
                throw new InvalidOperationException(
                    "Bootstrap is missing GameAudio. Add it on the scene, not from Startup.");
            }

            var bundle = AppFlowFactory.Create(this, scenes, audio);
            _fsm = bundle.StateMachine;
            _intents = bundle.Intents as IDisposable;

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

        void Update()
        {
            _fsm?.Tick();
        }

        void OnDestroy()
        {
            _intents?.Dispose();
        }
    }
}
