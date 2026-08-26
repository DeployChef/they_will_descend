using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Simulation.Io;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;

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
            SimCommands.TryPost(SimClockCommand.InGame(false));
            _audio?.StopSessionMusic();
        }

        public void Tick()
        {
            if (!SimWorld.TryGet(out var em, out var bag)
                || em.GetComponentData<SimControl>(bag).SessionInGame == 0)
                TryBeginSession();

            if (!_intents.ConsumePauseToggle())
                return;

            if (GameplayEscapeRouter.Active != null && GameplayEscapeRouter.Active.TryHandleEscape())
                return;

            SimCommands.TryPost(SimClockCommand.TogglePause());
        }

        static void TryBeginSession()
        {
            SimCommands.TryPost(SimClockCommand.InGame(true));
        }
    }
}
