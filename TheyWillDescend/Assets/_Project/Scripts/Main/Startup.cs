using System;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Shell;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// TEMPORARY (until 08 Ф4 GameEntry): Root's entry point. Takes the shell from the
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

            var shell = app.Container.Resolve<ShellService>();
            if (skipMenuToGameTemporarily)
            {
                GameLog.Warning("TEMPORARY: skipMenuToGame — starting a normal run (MainMenu not loaded).");
                shell.EnterGame(RunLaunch.Normal);
                return;
            }

            OpenMenu(shell).Forget();
        }

        static async UniTaskVoid OpenMenu(ShellService shell)
        {
            // Сцена, оставленная открытой в редакторе, приходит в Play уже загруженной.
            // Её никто не загружал, поэтому путь, который гасит занавес (старт рана), до неё
            // не дойдёт — Loading висел бы до конца сессии.
            if (shell.IsLoadingVisible || shell.IsGameSceneLoaded)
            {
                GameLog.Warning(
                    "Startup: Loading/Game are already loaded — extra scenes are open in the " +
                    "editor. Keep only Root in the Hierarchy.");
            }

            await shell.CloseLeftoverScenes();
            await shell.OpenMainMenu();
            GameLog.Info("Startup ready (Root). Menu opened.");
        }
    }
}
