using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Shell;
using UnityEngine;
using VContainer;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// Lives in the MainMenu scene. Owns the splash, then the button panel, and the menu input map
    /// while the scene lives; asks for the menu music on entry. Reveal is instant until menu animations exist.
    /// </summary>
    public sealed class MainMenuFlow : MonoBehaviour
    {
        [SerializeField] PressAnyKeyScreen splash;
        [SerializeField] MainMenuScreen menu;

        bool _menuVisible;
        ShellService _shell;
        InputService _input;

        [Inject]
        public void Construct(ShellService shell, InputService input, AudioService audio)
        {
            _shell = shell;
            _input = input;
            _input.Proceeded += OnProceed;
            _input.EnableMenu();
            audio.Music.Set(MusicTheme.Menu);

            if (shell.SplashShown)
                ShowMenu();
            else
                ShowSplash();
        }

        void OnDestroy()
        {
            if (_input == null)
                return;
            _input.Proceeded -= OnProceed;
            _input.Disable();
        }

        void OnProceed()
        {
            if (!_menuVisible)
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
            _shell.MarkSplashShown();
            if (splash != null)
                splash.Hide();
            if (menu != null)
                menu.Show();
        }
    }
}
