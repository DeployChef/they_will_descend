using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Entry point of the Game scene. On activation it takes the launch (or Normal when Game was
    /// opened directly) and reports the run start as loading work.
    /// Ф4 of 08 folds <see cref="GameRun"/> in here and adds the editor launch setting.
    /// </summary>
    public sealed class GameEntry : IInitializable, IDisposable
    {
        readonly ShellService _shell;
        readonly TransitionService _transition;
        readonly LoadingTasks _loading;
        readonly GameSession _session;
        readonly CancellationTokenSource _life = new();
        IDisposable _teardown;

        public GameEntry(ShellService shell, TransitionService transition, LoadingTasks loading, GameSession session)
        {
            _shell = shell;
            _transition = transition;
            _loading = loading;
            _session = session;
        }

        public void Initialize()
        {
            // TEMPORARY until 07 Ф1: the ECS world outlives the scene, so the run is reset
            // asynchronously before Game unloads. Correct shape: sync wipe in the scope's Dispose.
            _teardown = _transition.RegisterTeardown(_session.Shutdown);

            var launch = _shell.TryTakeLaunch(out var taken) ? taken : RunLaunch.Normal;
            _loading.Add(BeginRun(launch, _life.Token), weight: 3f, label: "Preparing the city...");
        }

        public void Dispose()
        {
            _teardown?.Dispose();
            _teardown = null;
            _life.Cancel();
            _life.Dispose();
        }

        async UniTask BeginRun(RunLaunch launch, CancellationToken ct)
        {
            if (!await _session.Begin(launch, ct))
                throw new InvalidOperationException("The run did not start (see GameSession errors above).");
        }
    }
}
