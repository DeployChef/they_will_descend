using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.Audio;

namespace TheyWillDescend.Shell.States
{
    public sealed class PlayingState : IAppState
    {
        readonly SimGate _simGate;
        readonly IShellIntentSource _intents;
        readonly GameAudio _audio;

        public AppStateId Id => AppStateId.Playing;

        public PlayingState(SimGate simGate, IShellIntentSource intents, GameAudio audio)
        {
            _simGate = simGate;
            _intents = intents;
            _audio = audio;
        }

        public void Enter()
        {
            _simGate.SetSessionInGame(true);
            _audio?.StartSessionMusic();
            GameLog.Info("Playing: Esc pauses time (stay in Playing).");
        }

        public void Exit()
        {
            _audio?.StopSessionMusic();
        }

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
