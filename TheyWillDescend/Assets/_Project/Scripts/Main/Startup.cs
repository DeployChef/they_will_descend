using System;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Shell;
using UnityEngine;
using VContainer;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// Builds the root container, then opens the menu or a run.
    /// </summary>
    public sealed class Startup : MonoBehaviour
    {
        [Header("Temporary debug")]
        [SerializeField] bool skipMenuToGameTemporarily;

        [SerializeField] RootLifetimeScope rootScope;

        bool _started;

        void Awake()
        {
            if (_started)
                return;
            _started = true;

            if (rootScope == null)
            {
                GameLog.Error("Startup: RootLifetimeScope must be assigned.");
                throw new InvalidOperationException(
                    "Startup is missing RootLifetimeScope.");
            }

            rootScope.Build();

            var shell = rootScope.Container.Resolve<ShellService>();
            if (skipMenuToGameTemporarily)
            {
                GameLog.Warning("TEMPORARY: skipMenuToGame — starting a normal run (MainMenu not loaded).");
                shell.EnterGame(RunLaunch.Normal);
                return;
            }

            shell.OpenMainMenu()
                .ContinueWith(() => GameLog.Info("Startup ready (Root). Menu opened."))
                .Forget();
        }
    }
}
