using Cysharp.Threading.Tasks;

namespace TheyWillDescend.Presentation.Shell
{
    /// <summary>
    /// Navigation only: where the player goes next and with which launch.
    /// Does not know <see cref="GameSession"/> and does not load scenes itself.
    /// </summary>
    public sealed class ShellService
    {
        readonly TransitionService _transition;
        RunLaunch? _pending;

        public ShellService(TransitionService transition)
        {
            _transition = transition;
        }

        /// <summary>Press any key уже пройден в этом процессе — меню открывается сразу с кнопками.</summary>
        public bool SplashShown { get; private set; }

        public void MarkSplashShown() => SplashShown = true;

        public UniTask<bool> StartRun(RunLaunch launch) => GoToGame(launch);

        /// <summary>Restart or load a slot from inside a run: the same pipeline, Game again.</summary>
        public UniTask<bool> RestartRun(RunLaunch launch) => GoToGame(launch);

        public UniTask<bool> GoToMenu()
        {
            if (_transition.IsRunning)
                return UniTask.FromResult(false);
            _pending = null;
            return _transition.Run(GameScenes.MainMenu);
        }

        /// <summary>Game's entry point takes the launch once. False when Game was opened directly.</summary>
        public bool TryTakeLaunch(out RunLaunch launch)
        {
            launch = _pending ?? default;
            var had = _pending.HasValue;
            _pending = null;
            return had;
        }

        async UniTask<bool> GoToGame(RunLaunch launch)
        {
            if (_transition.IsRunning)
                return false;
            _pending = launch;
            var ok = await _transition.Run(GameScenes.Game);
            if (!ok)
                _pending = null;
            return ok;
        }
    }
}
