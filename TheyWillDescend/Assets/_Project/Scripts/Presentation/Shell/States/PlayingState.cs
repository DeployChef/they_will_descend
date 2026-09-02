using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.App;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Presentation.GameHud;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Simulation.Session;

namespace TheyWillDescend.Shell.States
{
    public sealed class PlayingState : IAppState
    {
        readonly AppStateMachine _fsm;
        readonly GameSession _session;
        readonly GameInput _input;
        readonly GameAudio _audio;
        PauseMenuScreen _screen;
        CancellationTokenSource _loadCts;
        bool _busy;

        public AppStateId Id => AppStateId.Playing;

        public PlayingState(AppStateMachine fsm, GameSession session, GameInput input, GameAudio audio)
        {
            _fsm = fsm;
            _session = session;
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

            _busy = false;
            _screen = PauseMenuScreen.Current;
            if (_screen == null)
                GameLog.Error("PlayingState: PauseMenuScreen missing. Put it on PauseMenuPanel in Game.");
            else
            {
                _screen.Hide();
                _screen.ContinueClicked += ClosePauseMenu;
                _screen.SaveClicked += OnSaveClicked;
                _screen.LoadClicked += OnLoadClicked;
                _screen.MainMenuClicked += OnMainMenuClicked;
                _screen.ToggleRequested += TogglePauseMenu;
            }

            _input.PausePressed += OnPausePressed;
            _input.EnableGame();
            GameLog.Info("Playing: Esc opens the pause overlay (stay in Playing).");
        }

        public void Exit()
        {
            _input.PausePressed -= OnPausePressed;
            _input.Disable();
            CancelLoad();
            if (_screen != null)
            {
                _screen.ContinueClicked -= ClosePauseMenu;
                _screen.SaveClicked -= OnSaveClicked;
                _screen.LoadClicked -= OnLoadClicked;
                _screen.MainMenuClicked -= OnMainMenuClicked;
                _screen.ToggleRequested -= TogglePauseMenu;
                _screen.Hide();
            }

            _screen = null;
            _busy = false;
            SimCommands.TryPost(SimClockCommand.InGame(false));
            _audio?.StopSessionMusic();
        }

        void OnPausePressed()
        {
            if (_busy)
                return;
            if (BuildWidget.Current != null && BuildWidget.Current.TryHandleEscape())
                return;

            TogglePauseMenu();
        }

        void TogglePauseMenu()
        {
            if (_busy)
                return;
            if (_screen == null)
            {
                SimCommands.TryPost(SimClockCommand.TogglePause());
                return;
            }

            if (_screen.IsOpen)
                ClosePauseMenu();
            else
                OpenPauseMenu();
        }

        void OpenPauseMenu()
        {
            _screen.CloseBuildIfBusy();
            _screen.Show();
            SimCommands.TryPost(SimClockCommand.PlayerPaused(true));
        }

        void ClosePauseMenu()
        {
            if (_screen != null)
                _screen.Hide();
            SimCommands.TryPost(SimClockCommand.PlayerPaused(false));
        }

        void OnSaveClicked()
        {
            if (_busy)
                return;
            _screen?.CloseBuildIfBusy();
            RunSnapshotStore.Write(RunSessionSnapshot.Capture());
        }

        void OnLoadClicked()
        {
            if (_busy)
                return;
            if (!RunSnapshotStore.TryRead(out var snapshot))
                return;

            _busy = true;
            _screen?.CloseBuildIfBusy();
            _screen?.Hide();
            _input.Disable();
            CancelLoad();
            _loadCts = new CancellationTokenSource();
            LoadSlot(snapshot, _loadCts.Token).Forget();
        }

        void OnMainMenuClicked()
        {
            if (_busy)
                return;
            _busy = true;
            _screen?.Hide();
            _fsm.TransitionTo(AppStateId.ReturningToMenu);
        }

        async UniTaskVoid LoadSlot(RunSnapshot snapshot, CancellationToken cancellationToken)
        {
            var ready = false;
            try
            {
                await _session.RunWithLoadingAsync(
                    async ct =>
                    {
                        if (!RunSessionSnapshot.BeginApply(snapshot))
                            return;
                        if (!await _session.WaitForPhaseAsync(SimSessionPhase.Ready, ct))
                            return;

                        if (!SimCommands.TryPost(SimClockCommand.InGame(true)))
                            return;
                        _screen?.RebuildViews();
                        ready = true;
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _busy = false;
                if (ready && _fsm.CurrentId == AppStateId.Playing)
                    _input.EnableGame();
                else if (!cancellationToken.IsCancellationRequested)
                {
                    GameLog.Error("Playing load failed — ECS did not reach Ready; input remains disabled.");
                    if (_fsm.CurrentId == AppStateId.Playing)
                        _fsm.TransitionTo(AppStateId.ReturningToMenu);
                }
            }
        }

        void CancelLoad()
        {
            if (_loadCts == null)
                return;
            _loadCts.Cancel();
            _loadCts.Dispose();
            _loadCts = null;
        }
    }
}
