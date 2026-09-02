using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Shell;
using TheyWillDescend.Shell.States;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// Composition root helper: registers Shell states. Does not Find UI —
    /// MainMenu screens register themselves when that scene is loaded.
    /// </summary>
    public static class AppFlowFactory
    {
        public static AppStateMachine Create(GameSession session, GameAudio audio, GameInput input)
        {
            var fsm = new AppStateMachine();
            fsm.Register(new PressAnyKeyState(fsm, input));
            fsm.Register(new MainMenuState(fsm, input, session));
            fsm.Register(new LoadingGameState(fsm, session, input));
            fsm.Register(new PlayingState(fsm, session, input, audio));
            fsm.Register(new ReturningToMenuState(fsm, session, input));
            return fsm;
        }
    }
}
