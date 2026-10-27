using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Shell input for the process. Works on a runtime clone of TheyWillDescend.inputactions:
    /// the project asset is not enabled in Play Mode. Maps: Menu/Proceed, Game/Pause.
    /// The scene entry point picks the map; the process only owns the actions.
    /// </summary>
    public sealed class InputService : IDisposable
    {
        const string ProceedPath = "Menu/Proceed";
        const string PausePath = "Game/Pause";

        readonly InputActionAsset _runtime;
        readonly InputAction _proceed;
        readonly InputAction _pause;

        public event Action Proceeded;
        public event Action PausePressed;

        public InputService(InputActionAsset actions)
        {
            if (actions == null)
                throw new InvalidOperationException("InputService: assign TheyWillDescend.inputactions on AppScope.");

            _runtime = Object.Instantiate(actions);
            _proceed = _runtime.FindAction(ProceedPath, throwIfNotFound: true);
            _pause = _runtime.FindAction(PausePath, throwIfNotFound: true);
            _proceed.performed += OnProceed;
            _pause.performed += OnPause;
            _runtime.Disable();
        }

        public void EnableMenu()
        {
            _runtime.Disable();
            _proceed.actionMap.Enable();
        }

        public void EnableGame()
        {
            _runtime.Disable();
            _pause.actionMap.Enable();
        }

        public void Disable() => _runtime.Disable();

        public void Dispose()
        {
            _proceed.performed -= OnProceed;
            _pause.performed -= OnPause;
            _runtime.Disable();
            Object.Destroy(_runtime);
        }

        void OnProceed(InputAction.CallbackContext _) => Proceeded?.Invoke();

        void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
    }
}
