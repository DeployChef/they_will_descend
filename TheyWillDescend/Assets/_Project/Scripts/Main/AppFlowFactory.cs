using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Shell;
using TheyWillDescend.Shell.States;
using UnityEngine;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// Wires Shell graph. Resolves presentation ports after scenes are loaded.
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

        public static Bundle? Create(MonoBehaviour coroutineHost, SceneLoader scenes = null)
        {
            scenes ??= new SceneLoader();

            var ui = ResolveShellUi();
            if (ui == null)
            {
                GameLog.Error("AppFlowFactory: IShellUi not found. Add ShellUiBinder to MainMenu scene.");
                return null;
            }

            var simGate = new SimGate();
            simGate.BindAsActive();

            var session = new GameSession(scenes, coroutineHost);
            var intents = InputSystemShellIntents.CreateDefault();
            var fsm = new AppStateMachine();

            fsm.Register(new PressAnyKeyState(fsm, simGate, intents, ui));
            fsm.Register(new MainMenuState(fsm, simGate, ui));
            fsm.Register(new LoadingGameState(fsm, simGate, session, ui));
            fsm.Register(new PlayingState(fsm, simGate, intents));
            fsm.Register(new PausedState(fsm, simGate, intents));

            return new Bundle(simGate, fsm, intents, session);
        }

        static IShellUi ResolveShellUi()
        {
            return Object.FindFirstObjectByType<ShellUiBinder>();
        }
    }
}
