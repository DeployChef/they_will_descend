using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Shell;
using TheyWillDescend.Simulation.Session;

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
            SimCommands.TryPost(SimClockCommand.InGame(true));
            _audio?.StopSessionMusic();
            _input.EnableGame();

            var screen = PauseMenuScreen.Current;
            if (screen == null)
                GameLog.Error("PlayingState: PauseMenuScreen missing. Put it on PauseMenuPanel in Game.");
            else
                screen.Use(_input);

            GameLog.Info("Playing: Esc opens the pause overlay (stay in Playing).");
        }

        public void Exit()
        {
            PauseMenuScreen.Current?.Release();
            _input.Disable();
            SimCommands.TryPost(SimClockCommand.InGame(false));
            _audio?.StopSessionMusic();
        }
    }
}
