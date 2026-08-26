using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Infrastructure.Logging;
using UnityEngine.SceneManagement;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Narrow scene port: load/unload shell and session scenes. No economy knowledge.
    /// </summary>
    public sealed class SceneLoader
    {
        public bool IsLoaded(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        public bool IsGameLoaded => IsLoaded(GameScenes.Game);
        public bool IsMainMenuLoaded => IsLoaded(GameScenes.MainMenu);
        public bool IsLoadingLoaded => IsLoaded(GameScenes.Loading);

        public async UniTask LoadAdditive(
            string sceneName,
            bool setActive = false,
            CancellationToken cancellationToken = default)
        {
            if (IsLoaded(sceneName))
            {
                GameLog.Info($"Scene '{sceneName}' already loaded.");
                if (setActive)
                    TrySetActive(sceneName);
                return;
            }

            GameLog.Info($"Loading '{sceneName}' additive…");
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                GameLog.Error($"Failed to load '{sceneName}'. Add it to Build Settings.");
                return;
            }

            await op.ToUniTask(cancellationToken: cancellationToken);

            if (setActive)
                TrySetActive(sceneName);

            GameLog.Info($"Scene '{sceneName}' loaded.");
        }

        public async UniTask Unload(string sceneName, CancellationToken cancellationToken = default)
        {
            if (!IsLoaded(sceneName))
                return;

            GameLog.Info($"Unloading '{sceneName}'…");
            var op = SceneManager.UnloadSceneAsync(sceneName);
            if (op == null)
                return;

            await op.ToUniTask(cancellationToken: cancellationToken);
        }

        public UniTask LoadGameAdditive(CancellationToken cancellationToken = default) =>
            LoadAdditive(GameScenes.Game, setActive: true, cancellationToken);

        public UniTask UnloadGame(CancellationToken cancellationToken = default) =>
            Unload(GameScenes.Game, cancellationToken);

        public UniTask LoadMainMenuAdditive(CancellationToken cancellationToken = default) =>
            LoadAdditive(GameScenes.MainMenu, setActive: false, cancellationToken);

        public UniTask UnloadMainMenu(CancellationToken cancellationToken = default) =>
            Unload(GameScenes.MainMenu, cancellationToken);

        public UniTask LoadLoadingAdditive(CancellationToken cancellationToken = default) =>
            LoadAdditive(GameScenes.Loading, setActive: false, cancellationToken);

        public UniTask UnloadLoading(CancellationToken cancellationToken = default) =>
            Unload(GameScenes.Loading, cancellationToken);

        static void TrySetActive(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid())
                SceneManager.SetActiveScene(scene);
        }
    }
}
