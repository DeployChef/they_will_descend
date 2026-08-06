using System;

namespace _Project.Scripts.Shell
{
    /// <summary>
    /// Port Shell states use for menu/splash presentation.
    /// Startup does not know the concrete UI — factory resolves this after MainMenu loads.
    /// </summary>
    public interface IShellUi
    {
        event Action StartGameClicked;

        void ShowPressAnyKey();
        void ShowMainMenu();
        void ShowGameplayHud();
        void HideAll();
    }
}
