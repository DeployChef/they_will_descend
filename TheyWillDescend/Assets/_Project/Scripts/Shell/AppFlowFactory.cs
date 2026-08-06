using _Project.Scripts.Presentation.ShellUi;
using _Project.Scripts.Shell.States;

namespace _Project.Scripts.Shell
{
    /// <summary>
    /// Wires Shell graph. Keeps <see cref="Startup"/> thin.
    /// </summary>
    public static class AppFlowFactory
    {
        public readonly struct Bundle
        {
            public readonly SimGate SimGate;
            public readonly AppStateMachine StateMachine;
            public readonly IShellIntentSource Intents;

            public Bundle(SimGate simGate, AppStateMachine stateMachine, IShellIntentSource intents)
            {
                SimGate = simGate;
                StateMachine = stateMachine;
                Intents = intents;
            }
        }

        public static Bundle Create(ShellUiBinder ui)
        {
            var simGate = new SimGate();
            simGate.BindAsActive();

            var intents = InputSystemShellIntents.CreateDefault();
            var fsm = new AppStateMachine();
            fsm.Register(new PressAnyKeyState(fsm, simGate, intents, ui));
            fsm.Register(new MainMenuState(fsm, simGate, ui));
            fsm.Register(new PlayingState(fsm, simGate, intents, ui));
            fsm.Register(new PausedState(fsm, simGate, intents));

            return new Bundle(simGate, fsm, intents);
        }
    }
}
