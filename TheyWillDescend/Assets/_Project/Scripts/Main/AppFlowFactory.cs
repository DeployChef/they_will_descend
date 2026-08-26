using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Shell;
using TheyWillDescend.Shell.States;
using UnityEngine;

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
            public readonly IShellIntentSource Intents;
            public readonly GameSession Session;

            public Bundle(
                AppStateMachine stateMachine,
                IShellIntentSource intents,
                GameSession session)
            {
                StateMachine = stateMachine;
                Intents = intents;
                Session = session;
            }
        }

        public static Bundle Create(MonoBehaviour coroutineHost, SceneLoader scenes, GameAudio audio)
        {
            var session = new GameSession(scenes, coroutineHost);
            var intents = InputSystemShellIntents.CreateDefault();
            var fsm = new AppStateMachine();

            fsm.Register(new PressAnyKeyState(fsm, intents));
            fsm.Register(new MainMenuState(fsm));
            fsm.Register(new LoadingGameState(fsm, session));
            fsm.Register(new PlayingState(intents, audio));

            return new Bundle(fsm, intents, session);
        }
    }
}
