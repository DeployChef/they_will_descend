using System;
using System.Collections;
using _Project.Scripts.Infrastructure.Logging;
using UnityEngine;

namespace _Project.Scripts.Shell
{
    /// <summary>
    /// Boot/Root entry. Loads shell scenes and starts AppFlow.
    /// Does not know concrete UI widgets — only that AppFlowFactory can resolve <see cref="IShellUi"/>.
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
            // Still need MainMenu briefly so AppFlowFactory can resolve IShellUi.
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
                GameLog.Warning(
                    LogChannel.Bootstrap,
                    "TEMPORARY: skipMenuToGame — starting at LoadingGame.");
                _fsm.Start(AppStateId.LoadingGame);
            }
            else
            {
                GameLog.Info(LogChannel.Bootstrap, "Startup ready (Root). AppFlow started.");
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
            SimGate.ClearActive();
        }
    }
}
