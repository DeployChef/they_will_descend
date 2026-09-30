using TheyWillDescend.Shell;

namespace TheyWillDescend.Shell.States
{
    /// <summary>
    /// App is on the main menu. The panel is visible because the MainMenu scene is loaded.
    /// Menu buttons are UI; this state does not touch input.
    /// </summary>
    public sealed class MainMenuState : IAppState
    {
        public AppStateId Id => AppStateId.MainMenu;

        public void Enter()
        {
        }

        public void Exit()
        {
        }
    }
}
