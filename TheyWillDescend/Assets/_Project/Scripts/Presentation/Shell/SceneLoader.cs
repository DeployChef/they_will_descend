using System.Collections;
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

        public IEnumerator LoadAdditive(string sceneName, bool setActive = false)
        {
            if (IsLoaded(sceneName))
            {
                GameLog.Info($"Scene '{sceneName}' already loaded.");
                if (setActive)
                    TrySetActive(sceneName);
                yield break;
            }

            GameLog.Info($"Loading '{sceneName}' additive…");
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                GameLog.Error($"Failed to load '{sceneName}'. Add it to Build Settings.");
                yield break;
            }

            while (!op.isDone)
                yield return null;

            if (setActive)
                TrySetActive(sceneName);

            GameLog.Info($"Scene '{sceneName}' loaded.");
        }

        public IEnumerator Unload(string sceneName)
        {
            if (!IsLoaded(sceneName))
                yield break;

            GameLog.Info($"Unloading '{sceneName}'…");
            var op = SceneManager.UnloadSceneAsync(sceneName);
            if (op == null)
                yield break;

            while (!op.isDone)
                yield return null;
        }

        public IEnumerator LoadGameAdditive() => LoadAdditive(GameScenes.Game, setActive: true);

        public IEnumerator UnloadGame() => Unload(GameScenes.Game);

        public IEnumerator LoadMainMenuAdditive() => LoadAdditive(GameScenes.MainMenu, setActive: false);

        public IEnumerator UnloadMainMenu() => Unload(GameScenes.MainMenu);

        public IEnumerator LoadLoadingAdditive() => LoadAdditive(GameScenes.Loading, setActive: false);

        public IEnumerator UnloadLoading() => Unload(GameScenes.Loading);

        static void TrySetActive(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid())
                SceneManager.SetActiveScene(scene);
        }
    }
}
