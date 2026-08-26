using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Runtime copy of the project Input Action Asset. Enable one map per app state.
    /// Callbacks, not polling.
    /// </summary>
    public sealed class GameInput : IDisposable
    {
        readonly InputActionAsset _asset;
        readonly InputActionMap _menu;
        readonly InputActionMap _game;
        readonly InputAction _proceed;
        readonly InputAction _pause;

        public event Action Proceeded;
        public event Action PausePressed;

        public GameInput(InputActionAsset source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            _asset = UnityEngine.Object.Instantiate(source);
            _menu = _asset.FindActionMap("Menu", throwIfNotFound: true);
            _game = _asset.FindActionMap("Game", throwIfNotFound: true);
            _proceed = _menu.FindAction("Proceed", throwIfNotFound: true);
            _pause = _game.FindAction("Pause", throwIfNotFound: true);

            _proceed.performed += OnProceed;
            _pause.performed += OnPause;
        }

        public void EnableMenu()
        {
            _game.Disable();
            _menu.Enable();
        }

        public void EnableGame()
        {
            _menu.Disable();
            _game.Enable();
        }

        public void Disable()
        {
            _menu.Disable();
            _game.Disable();
        }

        public void Dispose()
        {
            _proceed.performed -= OnProceed;
            _pause.performed -= OnPause;
            Disable();
            if (_asset != null)
                UnityEngine.Object.Destroy(_asset);
        }

        void OnProceed(InputAction.CallbackContext _) => Proceeded?.Invoke();

        void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
    }
}
