using TheyWillDescend.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// Lives in the MainMenu scene. Owns the splash, then the button panel.
    /// Reveal is instant until menu animations exist.
    /// </summary>
    public sealed class MainMenuFlow : MonoBehaviour
    {
        [SerializeField] PressAnyKeyScreen splash;
        [SerializeField] MainMenuScreen menu;

        bool _menuVisible;

        [Inject]
        public void Construct(AppContext context)
        {
            if (context.IsFirstStart)
                ShowMenu();
            else
                ShowSplash();
        }

        void Update()
        {
            if (_menuVisible || !WasProceedPressed())
                return;

            ShowMenu();
        }

        void ShowSplash()
        {
            _menuVisible = false;
            if (splash != null)
                splash.Show();
            if (menu != null)
                menu.Hide();
        }

        void ShowMenu()
        {
            _menuVisible = true;
            if (splash != null)
                splash.Hide();
            if (menu != null)
                menu.Show();
        }

        static bool WasProceedPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                return true;

            var mouse = Mouse.current;
            if (mouse != null
                && (mouse.leftButton.wasPressedThisFrame
                    || mouse.rightButton.wasPressedThisFrame
                    || mouse.middleButton.wasPressedThisFrame))
                return true;

            var pad = Gamepad.current;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }
    }
}
