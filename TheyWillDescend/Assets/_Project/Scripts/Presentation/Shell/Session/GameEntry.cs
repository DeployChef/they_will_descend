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
        readonly TransitionService _transition;
        readonly LoadingTasks _loading;
        readonly GameSession _session;
        readonly InputService _input;
        readonly PauseMenuScreen _pause;
        readonly AudioService _audio;
        readonly EditorRunLaunch _editorLaunch;
#if UNITY_EDITOR
        readonly float _debugLoadStageSeconds;
#endif
        readonly CancellationTokenSource _life = new();
        IDisposable _teardown;
        IDisposable _worldBanks;
        bool _live;

        public GameEntry(
            ShellService shell,
            TransitionService transition,
            LoadingTasks loading,
            GameSession session,
            InputService input,
            PauseMenuScreen pause,
            AudioService audio,
            EditorRunLaunch editorLaunch,
            float debugLoadStageSeconds)
        {
            _shell = shell;
            _transition = transition;
            _loading = loading;
            _session = session;
            _input = input;
            _pause = pause;
            _audio = audio;
            _editorLaunch = editorLaunch;
#if UNITY_EDITOR
            _debugLoadStageSeconds = debugLoadStageSeconds;
#endif
        }

        public void Initialize()
        {
            _teardown = _transition.RegisterTeardown(EndRun);

#if UNITY_EDITOR
            if (_debugLoadStageSeconds > 0.001f)
            {
                _loading.Add(DebugLoadStage(0), weight: 1f, label: DebugLoadLabels[0]);
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
            _loading.Add(AcquireWorldBanks(_life.Token), weight: 1f, label: "Loading sounds...");
            _loading.Add(BeginRun(PickLaunch(), _life.Token), weight: 3f, label: "Preparing the city...");
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
                _loading.Add(DebugLoadStage(next), weight: 1f, label: DebugLoadLabels[next]);
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
            if (_shell.TryTakeLaunch(out var taken))
                return taken;
#if UNITY_EDITOR
            GameLog.Info($"GameEntry: no launch from the menu — editor setting {_editorLaunch}.");
            return _editorLaunch.ToLaunch();
#else
            return RunLaunch.Normal;
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
