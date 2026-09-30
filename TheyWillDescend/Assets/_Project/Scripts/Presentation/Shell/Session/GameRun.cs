using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Simulation.Session;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Live-run switch on the Game scene. Turns the sim clock and game input on
    /// once the session is ready, and off when the run is leaving.
    /// Not a state machine.
    /// </summary>
    public sealed class GameRun
    {
        readonly GameInput _input;
        readonly GameAudio _audio;
        bool _live;

        public GameRun(GameInput input, GameAudio audio)
        {
            _input = input;
            _audio = audio;
        }

        public bool IsLive => _live;

        public void Arm()
        {
            SimCommands.TryPost(SimClockCommand.InGame(true));
            _audio?.StopSessionMusic();
            _input.EnableGame();

            var screen = PauseMenuScreen.Current;
            if (screen == null)
                GameLog.Error("GameRun: PauseMenuScreen missing. Put it on PauseMenuPanel in Game.");
            else
                screen.Use(_input);

            _live = true;
        }

        public void Disarm()
        {
            if (!_live)
                return;

            _live = false;
            PauseMenuScreen.Current?.Release();
            _input.Disable();
            SimCommands.TryPost(SimClockCommand.InGame(false));
            _audio?.StopSessionMusic();
        }
    }
}
