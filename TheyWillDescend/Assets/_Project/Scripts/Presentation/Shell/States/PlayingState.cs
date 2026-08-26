using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Simulation.Io;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;

namespace TheyWillDescend.Shell.States
{
    public sealed class PlayingState : IAppState
    {
        readonly GameInput _input;
        readonly GameAudio _audio;

        public AppStateId Id => AppStateId.Playing;

        public PlayingState(GameInput input, GameAudio audio)
        {
            _input = input;
            _audio = audio;
        }

        public void Enter()
        {
            TryBeginSession();
            _audio?.StartSessionMusic();
            _input.PausePressed += OnPausePressed;
            _input.EnableGame();
            GameLog.Info("Playing: Esc pauses the city (stay in Playing).");
        }

        public void Exit()
        {
            _input.PausePressed -= OnPausePressed;
            _input.Disable();
            SimCommands.TryPost(SimClockCommand.InGame(false));
            _audio?.StopSessionMusic();
        }

        public void Tick()
        {
            if (!SimWorld.TryGet(out var em, out var bag)
                || em.GetComponentData<SimControl>(bag).SessionInGame == 0)
                TryBeginSession();
        }

        void OnPausePressed()
        {
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
