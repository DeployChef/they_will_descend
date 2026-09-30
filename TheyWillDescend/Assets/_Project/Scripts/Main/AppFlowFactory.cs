using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Shell;
using TheyWillDescend.Shell.States;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// Composition root helper: registers Shell states. Does not Find UI.
    /// The MainMenu scene owns its splash and buttons.
    /// </summary>
    public static class AppFlowFactory
    {
        public static AppStateMachine Create(GameSession session, GameAudio audio, GameInput input)
        {
            var fsm = new AppStateMachine();
            fsm.Register(new MainMenuState());
            fsm.Register(new LoadingGameState(fsm, session, input));
            fsm.Register(new PlayingState(input, audio));
            fsm.Register(new ReturningToMenuState(fsm, session, input));
            return fsm;
        }
    }
}
