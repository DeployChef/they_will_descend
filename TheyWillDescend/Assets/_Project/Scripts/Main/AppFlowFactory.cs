using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Shell;
using TheyWillDescend.Shell.States;
using UnityEngine.InputSystem;

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
            public readonly GameInput Input;
            public readonly GameSession Session;

            public Bundle(AppStateMachine stateMachine, GameInput input, GameSession session)
            {
                StateMachine = stateMachine;
                Input = input;
                Session = session;
            }
        }

        public static Bundle Create(SceneLoader scenes, GameAudio audio, InputActionAsset inputActions)
        {
            var session = new GameSession(scenes);
            var input = new GameInput(inputActions);
            var fsm = new AppStateMachine();

            fsm.Register(new PressAnyKeyState(fsm, input));
            fsm.Register(new MainMenuState(fsm, input));
            fsm.Register(new LoadingGameState(fsm, session, input));
            fsm.Register(new PlayingState(input, audio));

            return new Bundle(fsm, input, session);
        }
    }
}
