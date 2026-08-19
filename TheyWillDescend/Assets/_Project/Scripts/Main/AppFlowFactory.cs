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
            public readonly SimGate SimGate;
            public readonly AppStateMachine StateMachine;
            public readonly IShellIntentSource Intents;
            public readonly GameSession Session;

            public Bundle(
                SimGate simGate,
                AppStateMachine stateMachine,
                IShellIntentSource intents,
                GameSession session)
            {
                SimGate = simGate;
                StateMachine = stateMachine;
                Intents = intents;
                Session = session;
            }
        }

        public static Bundle Create(MonoBehaviour coroutineHost, SceneLoader scenes)
        {
            var simGate = new SimGate();
            simGate.BindAsActive();

            var session = new GameSession(scenes, coroutineHost);
            var intents = InputSystemShellIntents.CreateDefault();
            var fsm = new AppStateMachine();

            fsm.Register(new PressAnyKeyState(fsm, simGate, intents));
            fsm.Register(new MainMenuState(fsm, simGate));
            fsm.Register(new LoadingGameState(fsm, simGate, session));
            fsm.Register(new PlayingState(simGate, intents));

            return new Bundle(simGate, fsm, intents, session);
        }
    }
}
