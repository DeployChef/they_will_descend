using System;
using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Presentation.ShellUi;
using UnityEngine;

namespace _Project.Scripts.Shell
{
    /// <summary>
    /// Scene entry for Shell. Place on Game/Bootstrap — never inside Simulation SubScene.
    /// </summary>
    public sealed class Startup : MonoBehaviour
    {
        [SerializeField] ShellUiBinder shellUi;

        AppStateMachine _fsm;
        IDisposable _intents;
        bool _started;

        void Awake()
        {
            if (_started)
                return;
            _started = true;

            if (shellUi == null)
                shellUi = FindFirstObjectByType<ShellUiBinder>();

            if (shellUi == null)
            {
                GameLog.Error(LogChannel.Bootstrap, "Startup: ShellUiBinder missing on scene.");
                enabled = false;
                return;
            }

            var bundle = AppFlowFactory.Create(shellUi);
            _fsm = bundle.StateMachine;
            _intents = bundle.Intents as IDisposable;

            GameLog.Info(LogChannel.Bootstrap, "Startup ready.");
            _fsm.Start(AppStateId.PressAnyKey);
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
