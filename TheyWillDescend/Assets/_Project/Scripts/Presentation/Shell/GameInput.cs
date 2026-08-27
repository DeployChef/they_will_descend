using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Bootstrap host for shell input. Assign the asset and actions in the inspector.
    /// Runtime clone — the project asset is not enabled in Play Mode.
    /// </summary>
    public sealed class GameInput : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;
        [SerializeField] InputActionReference proceed;
        [SerializeField] InputActionReference pause;

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

            if (proceed == null || pause == null)
            {
                throw new InvalidOperationException(
                    "GameInput: assign Proceed and Pause in the inspector.");
            }

            _runtime = Instantiate(actions);
            _proceed = Resolve(_runtime, proceed);
            _pause = Resolve(_runtime, pause);
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

        static InputAction Resolve(InputActionAsset runtime, InputActionReference reference)
        {
            var source = reference.action;
            if (source == null)
            {
                throw new InvalidOperationException(
                    "GameInput: action reference is empty. Drag the action from the .inputactions asset.");
            }

            var action = runtime.FindAction(source.id);
            if (action == null)
            {
                throw new InvalidOperationException(
                    $"GameInput: action '{source.name}' is not on the assigned asset.");
            }

            return action;
        }

        void OnProceed(InputAction.CallbackContext _) => Proceeded?.Invoke();

        void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
    }
}
