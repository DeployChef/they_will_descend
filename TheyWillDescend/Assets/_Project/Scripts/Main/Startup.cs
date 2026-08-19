using System;
using System.Collections;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Shell;
using UnityEngine;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// Composition root. Lives on Bootstrap. The only assembly that may see both Shell and Presentation.
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
            yield return scenes.LoadMainMenuAdditive();

            var bundle = AppFlowFactory.Create(this, scenes);
            if (bundle == null)
            {
                enabled = false;
                yield break;
            }

            _fsm = bundle.Value.StateMachine;
            _intents = bundle.Value.Intents as IDisposable;

            if (skipMenuToGameTemporarily)
            {
                GameLog.Warning("TEMPORARY: skipMenuToGame — starting at LoadingGame.");
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
            SimGate.Active?.PushClock(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            _intents?.Dispose();
            SimGate.ClearActive();
        }
    }
}
