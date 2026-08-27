using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Shell;
using TheyWillDescend.Shell.States;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// Composition root helper: registers Shell states. Does not Find UI —
    /// MainMenu binder registers <see cref="ShellUiPort"/> when that scene is loaded.
    /// </summary>
    public static class AppFlowFactory
    {
        public readonly struct Bundle
        {
            public readonly AppStateMachine StateMachine;
            public readonly GameSession Session;

            public Bundle(AppStateMachine stateMachine, GameSession session)
            {
                StateMachine = stateMachine;
                Session = session;
            }
        }

        public static Bundle Create(SceneLoader scenes, GameAudio audio, GameInput input)
        {
            var session = new GameSession(scenes);
            var fsm = new AppStateMachine();

            fsm.Register(new PressAnyKeyState(fsm, input));
            fsm.Register(new MainMenuState(fsm, input));
            fsm.Register(new LoadingGameState(fsm, session, input));
            fsm.Register(new PlayingState(input, audio));

            return new Bundle(fsm, session);
        }
    }
}
