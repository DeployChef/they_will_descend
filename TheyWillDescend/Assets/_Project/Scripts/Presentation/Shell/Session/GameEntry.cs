using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Presentation.Infrastructure;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Simulation.Session;
using VContainer.Unity;

namespace TheyWillDescend.Presentation.Shell
{
    /// <summary>
    /// Entry point of the Game scene. Picks the launch (menu → editor setting → Normal), reports the
    /// world sound banks and the run start as loading work, then turns the clock, game input, pause menu
    /// and gameplay music on.
    /// Leaving the scene turns them off before the run is reset.
    /// </summary>
    public sealed class GameEntry : IInitializable, IDisposable
    {
#if UNITY_EDITOR
        static readonly string[] DebugLoadLabels =
        {
            "Gathering tribute...",
            "Carving the plaza...",
            "Calling the workers...",
            "Waking the pyramid...",
            "Binding the era..."
        };
#endif

        readonly ShellService _shell;
        readonly AppContext _app;
        readonly LoadingScreen _screen;
        readonly GameSession _session;
        readonly InputService _input;
        readonly PauseMenuScreen _pause;
        readonly AudioService _audio;
#if UNITY_EDITOR
        readonly string _editorSaveId;
        readonly bool _editorDebug;
        readonly float _debugLoadStageSeconds;
#endif
        readonly CancellationTokenSource _life = new();
        IDisposable _teardown;
        IDisposable _worldBanks;
        bool _live;

        public GameEntry(
            ShellService shell,
            AppContext app,
            LoadingScreen screen,
            GameSession session,
            InputService input,
            PauseMenuScreen pause,
            AudioService audio,
            string editorSaveId,
            bool editorDebug,
            float debugLoadStageSeconds)
        {
            _shell = shell;
            _app = app;
            _screen = screen;
            _session = session;
            _input = input;
            _pause = pause;
            _audio = audio;
#if UNITY_EDITOR
            _editorSaveId = editorSaveId;
            _editorDebug = editorDebug;
            _debugLoadStageSeconds = debugLoadStageSeconds;
#endif
        }

        public void Initialize()
        {
            _teardown = _shell.RegisterTeardown(EndRun);

#if UNITY_EDITOR
            if (_debugLoadStageSeconds > 0.001f)
            {
                _screen.Add(DebugLoadStage(0), weight: 1f, label: DebugLoadLabels[0]);
                return;
            }
#endif
            QueueRunWork();
        }

        public void Dispose()
        {
            _teardown?.Dispose();
            _teardown = null;
            _worldBanks?.Dispose();
            _worldBanks = null;
            _life.Cancel();
            _life.Dispose();
        }

        void QueueRunWork()
        {
            _screen.Add(AcquireWorldBanks(_life.Token), weight: 1f, label: "Loading sounds...");
            _screen.Add(BeginRun(PickLaunch(), _life.Token), weight: 3f, label: "Preparing the city...");
        }

#if UNITY_EDITOR
        async UniTask DebugLoadStage(int index)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(_debugLoadStageSeconds),
                DelayType.UnscaledDeltaTime,
                cancellationToken: _life.Token);
            var next = index + 1;
            if (next < DebugLoadLabels.Length)
            {
                _screen.Add(DebugLoadStage(next), weight: 1f, label: DebugLoadLabels[next]);
                return;
            }

            QueueRunWork();
        }
#endif

        async UniTask AcquireWorldBanks(CancellationToken ct)
        {
            _worldBanks = await _audio.Banks.Acquire(_audio.Catalog.GameWorld, ct);
        }

        RunLaunch PickLaunch()
        {
            if (_app.CurrentLaunch.HasValue)
                return _app.CurrentLaunch.Value;
#if UNITY_EDITOR
            var launch = string.IsNullOrWhiteSpace(_editorSaveId)
                ? RunLaunch.New(_editorDebug)
                : RunLaunch.FromSave(_editorSaveId);
            GameLog.Info($"GameEntry: no launch from the menu — editor setting {launch}.");
            return launch;
#else
            return RunLaunch.New();
#endif
        }

        async UniTask BeginRun(RunLaunch launch, CancellationToken ct)
        {
            if (!await _session.Begin(launch, ct))
                throw new InvalidOperationException("The run did not start (see GameSession errors above).");
            Arm();
        }

        async UniTask<bool> EndRun(CancellationToken ct)
        {
            var wasLive = _live;
            Disarm();
            if (await _session.Shutdown(ct))
                return true;
            if (wasLive)
                Arm();
            return false;
        }

        void Arm()
        {
            SimCommands.TryPost(SimClockCommand.InGame(true));
            _input.EnableGame();
            _pause.Use();
            _audio.Music.Set(MusicTheme.Gameplay);
            _live = true;
        }

        void Disarm()
        {
            if (!_live)
                return;

            _live = false;
            _pause.Release();
            _input.Disable();
            SimCommands.TryPost(SimClockCommand.InGame(false));
        }
    }
}
