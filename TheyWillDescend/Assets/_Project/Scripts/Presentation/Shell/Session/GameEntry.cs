using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Presentation.Infrastructure;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Presentation.SimBridge;
using VContainer.Unity;

namespace TheyWillDescend.Presentation.Shell
{
    /// <summary>
    /// Entry point of the Game scene. Picks the launch (menu → editor setting → Normal), reports the
    /// world sound banks and <see cref="SimulationHost.Open"/> as loading work, then turns game input,
    /// the pause menu and gameplay music on. The host owns the run: it builds the session and wipes it
    /// when this scene dies.
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

        readonly AppContext _app;
        readonly LoadingScreen _screen;
        readonly SimulationHost _host;
        readonly InputService _input;
        readonly PauseMenuScreen _pause;
        readonly AudioService _audio;
#if UNITY_EDITOR
        readonly string _editorSaveId;
        readonly bool _editorDebug;
        readonly float _debugLoadStageSeconds;
#endif
        readonly CancellationTokenSource _life = new();
        IDisposable _worldBanks;
        bool _live;

        public GameEntry(
            AppContext app,
            LoadingScreen screen,
            SimulationHost host,
            InputService input,
            PauseMenuScreen pause,
            AudioService audio,
            string editorSaveId,
            bool editorDebug,
            float debugLoadStageSeconds)
        {
            _app = app;
            _screen = screen;
            _host = host;
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
#if UNITY_EDITOR
            if (_debugLoadStageSeconds > 0.001f)
                _screen.Add(DebugLoadStage(0), weight: 1f, label: DebugLoadLabels[0]);
            else
#endif
                QueueRunWork();

            // A menu transition already raised the curtain and will wait. Play on this scene has not.
            if (!_screen.IsVisible)
                CoverDirectOpen(_life.Token).Forget();
        }

        public void Dispose()
        {
            Disarm();
            _worldBanks?.Dispose();
            _worldBanks = null;
            _life.Cancel();
            _life.Dispose();
        }

        async UniTaskVoid CoverDirectOpen(CancellationToken ct)
        {
            _screen.ShowImmediate();
            try
            {
                await _screen.Wait(ct);
                await _screen.Hide(ct);
            }
            catch (OperationCanceledException)
            {
            }
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

        UniTask BeginRun(RunLaunch launch, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!_host.Open(launch))
                throw new InvalidOperationException("The run did not start (see SimulationHost errors above).");
            Arm();
            return UniTask.CompletedTask;
        }

        void Arm()
        {
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
        }
    }
}
