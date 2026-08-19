using TheyWillDescend.Infrastructure.Logging;

namespace TheyWillDescend.Shell.States
{
    public sealed class PlayingState : IAppState
    {
        readonly SimGate _simGate;
        readonly IShellIntentSource _intents;

        public AppStateId Id => AppStateId.Playing;

        public PlayingState(SimGate simGate, IShellIntentSource intents)
        {
            _simGate = simGate;
            _intents = intents;
        }

        public void Enter()
        {
            _simGate.SetSessionInGame(true);
            GameLog.Info("Playing: Esc pauses time (stay in Playing).");
        }

        public void Exit() { }

        public void Tick()
        {
            if (!_intents.ConsumePauseToggle())
                return;

            // Overlays (build catalog) consume Esc before time pause.
            if (GameplayEscapeRouter.Active != null && GameplayEscapeRouter.Active.TryHandleEscape())
                return;

            _simGate.TogglePlayerPause();
        }
    }
}
