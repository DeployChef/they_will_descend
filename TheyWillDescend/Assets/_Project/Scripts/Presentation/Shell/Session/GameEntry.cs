using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Simulation.Session;
using VContainer.Unity;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Entry point of the Game scene. Picks the launch (menu → editor setting → Normal), reports the
    /// run start as loading work, then turns the clock, game input and pause menu on.
    /// Leaving the scene turns them off before the run is reset.
    /// </summary>
    public sealed class GameEntry : IInitializable, IDisposable
    {
        readonly ShellService _shell;
        readonly TransitionService _transition;
        readonly LoadingTasks _loading;
        readonly GameSession _session;
        readonly GameInput _input;
        readonly PauseMenuScreen _pause;
        readonly EditorRunLaunch _editorLaunch;
        readonly CancellationTokenSource _life = new();
        IDisposable _teardown;
        bool _live;

        public GameEntry(
            ShellService shell,
            TransitionService transition,
            LoadingTasks loading,
            GameSession session,
            GameInput input,
            PauseMenuScreen pause,
            EditorRunLaunch editorLaunch)
        {
            _shell = shell;
            _transition = transition;
            _loading = loading;
            _session = session;
            _input = input;
            _pause = pause;
            _editorLaunch = editorLaunch;
        }

        public void Initialize()
        {
            // TEMPORARY until 07 Ф1: the ECS world outlives the scene, so the run is reset
            // asynchronously before Game unloads. Correct shape: sync wipe in the scope's Dispose.
            _teardown = _transition.RegisterTeardown(EndRun);

            _loading.Add(BeginRun(PickLaunch(), _life.Token), weight: 3f, label: "Preparing the city...");
        }

        public void Dispose()
        {
            _teardown?.Dispose();
            _teardown = null;
            _life.Cancel();
            _life.Dispose();
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
