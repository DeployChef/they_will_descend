using System;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// TEMPORARY (until 08 Ф4 removes Root): Root's entry point. Takes the shell from the
    /// process scope, then opens the menu or a run.
    /// </summary>
    public sealed class Startup : MonoBehaviour
    {
        [Header("Temporary debug")]
        [SerializeField] bool skipMenuToGameTemporarily;

        bool _started;

        void Awake()
        {
            if (_started)
                return;
            _started = true;

            var app = VContainerSettings.Instance != null
                ? VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance()
                : null;
            if (app == null || app.Container == null)
            {
                GameLog.Error("Startup: AppScope is missing. Assign it on VContainerSettings (Preloaded Assets).");
                throw new InvalidOperationException("Startup: AppScope is missing.");
            }

            var game = SceneManager.GetSceneByName(GameScenes.Game);
            if (game.IsValid() && game.isLoaded)
            {
                GameLog.Info("Startup: Game is open in the editor — it starts its own run.");
                return;
            }

            var shell = app.Container.Resolve<ShellService>();
            if (skipMenuToGameTemporarily)
            {
                GameLog.Warning("TEMPORARY: skipMenuToGame — starting a normal run (MainMenu not loaded).");
                shell.StartRun(RunLaunch.Normal).Forget();
                return;
            }

            shell.GoToMenu().Forget();
        }
    }
}
