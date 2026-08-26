using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Simulation.Io;

namespace TheyWillDescend.Shell.States
{
    public sealed class PlayingState : IAppState
    {
        readonly IShellIntentSource _intents;
        readonly GameAudio _audio;

        public AppStateId Id => AppStateId.Playing;

        public PlayingState(IShellIntentSource intents, GameAudio audio)
        {
            _intents = intents;
            _audio = audio;
        }

        public void Enter()
        {
            TryBeginSession();
            _audio?.StartSessionMusic();
            GameLog.Info("Playing: Esc pauses the city (stay in Playing).");
        }

        public void Exit()
        {
            SimIo.TryEnqueueSessionInGame(false);
            _audio?.StopSessionMusic();
        }

        public void Tick()
        {
            if (!SimIo.TryGetSimControl(out var control) || control.SessionInGame == 0)
                TryBeginSession();

            if (!_intents.ConsumePauseToggle())
                return;

            if (GameplayEscapeRouter.Active != null && GameplayEscapeRouter.Active.TryHandleEscape())
                return;

            SimIo.TryEnqueueTogglePlayerPause();
        }

        static void TryBeginSession()
        {
            SimIo.TryEnqueueSessionInGame(true);
        }
    }
}
