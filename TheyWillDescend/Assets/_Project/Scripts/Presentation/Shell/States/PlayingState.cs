using System;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Presentation.GameHud;
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
            try
            {
                _audio?.StartSessionMusic();
            }
            catch (Exception e)
            {
                GameLog.Error($"Playing: music failed to start. {e.Message}");
            }

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

        void OnPausePressed()
        {
            if (BuildWidget.Current != null && BuildWidget.Current.TryHandleEscape())
                return;

            SimCommands.TryPost(SimClockCommand.TogglePause());
        }
    }
}
