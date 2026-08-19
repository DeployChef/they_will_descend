using System;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Menu/splash presentation. Lives on MainMenu; bind via <see cref="ShellUiPort"/>.
    /// Gameplay HUD is not this port — it lives on the Game scene.
    /// </summary>
    public interface IShellUi
    {
        event Action StartGameClicked;

        void ShowPressAnyKey();
        void ShowMainMenu();
        void HideAll();
    }

    /// <summary>
    /// Scene-owned port. MainMenu binder binds in OnEnable / unbinds in OnDisable.
    /// States may use Current only while MainMenu is loaded.
    /// </summary>
    public static class ShellUiPort
    {
        public static IShellUi Current { get; private set; }

        public static void Bind(IShellUi ui) => Current = ui;

        public static void Unbind(IShellUi ui)
        {
            if (Current == ui)
                Current = null;
        }
    }
}
