using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Bootstrap host for shell input. Assign TheyWillDescend.inputactions.
    /// Runtime clone — the project asset is not enabled in Play Mode.
    /// Maps: Menu/Proceed, Game/Pause.
    /// </summary>
    public sealed class GameInput : MonoBehaviour
    {
        const string ProceedPath = "Menu/Proceed";
        const string PausePath = "Game/Pause";

        [SerializeField] InputActionAsset actions;

        InputActionAsset _runtime;
        InputAction _proceed;
        InputAction _pause;

        public event Action Proceeded;
        public event Action PausePressed;

        void Awake() => Bind();

        void OnDestroy() => Unbind();

        public void EnableMenu()
        {
            Bind();
            _runtime.Disable();
            _proceed.actionMap.Enable();
        }

        public void EnableGame()
        {
            Bind();
            _runtime.Disable();
            _pause.actionMap.Enable();
        }

        public void Disable()
        {
            if (_runtime != null)
                _runtime.Disable();
        }

        void Bind()
        {
            if (_runtime != null)
                return;

            if (actions == null)
            {
                throw new InvalidOperationException(
                    "GameInput: assign TheyWillDescend.inputactions on Bootstrap.");
            }

            _runtime = Instantiate(actions);
            _proceed = _runtime.FindAction(ProceedPath, throwIfNotFound: true);
            _pause = _runtime.FindAction(PausePath, throwIfNotFound: true);
            _proceed.performed += OnProceed;
            _pause.performed += OnPause;
            _runtime.Disable();
        }

        void Unbind()
        {
            if (_runtime == null)
                return;

            _proceed.performed -= OnProceed;
            _pause.performed -= OnPause;
            _runtime.Disable();
            Destroy(_runtime);
            _runtime = null;
        }

        void OnProceed(InputAction.CallbackContext _) => Proceeded?.Invoke();

        void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
    }
}
