using System;
using UnityEngine.InputSystem;

namespace _Project.Scripts.Shell
{
    /// <summary>
    /// New Input System map for Shell flow (splash + pause).
    /// Status: <b>temporary</b> map built in code — replace with .inputactions asset when HUD grows.
    /// Why temporary is OK: same interface (<see cref="IShellIntentSource"/>); asset swap won't touch states.
    /// </summary>
    public sealed class InputSystemShellIntents : IShellIntentSource, IDisposable
    {
        readonly InputActionMap _map;
        readonly InputAction _proceed;
        readonly InputAction _pauseToggle;

        public static InputSystemShellIntents CreateDefault()
        {
            var map = new InputActionMap("Shell");

            var proceed = map.AddAction("Proceed", type: InputActionType.Button);
            proceed.AddBinding("<Keyboard>/anyKey");
            proceed.AddBinding("<Gamepad>/buttonSouth");
            proceed.AddBinding("<Mouse>/leftButton");

            var pause = map.AddAction("PauseToggle", type: InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");

            map.Enable();
            return new InputSystemShellIntents(map, proceed, pause);
        }

        InputSystemShellIntents(InputActionMap map, InputAction proceed, InputAction pauseToggle)
        {
            _map = map;
            _proceed = proceed;
            _pauseToggle = pauseToggle;
        }

        public bool ConsumeProceed() => _proceed.WasPressedThisFrame();

        public bool ConsumePauseToggle() => _pauseToggle.WasPressedThisFrame();

        public void Dispose()
        {
            _map?.Disable();
            _map?.Dispose();
        }
    }
}
