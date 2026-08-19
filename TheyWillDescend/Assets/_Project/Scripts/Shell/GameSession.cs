using System;
using System.Collections;
using TheyWillDescend.Infrastructure.Logging;
using UnityEngine;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Gameplay session: load Game, then unload MainMenu (UI port gone — states must not use IShellUi after Start).
    /// </summary>
    public sealed class GameSession
    {
        readonly SceneLoader _scenes;
        readonly MonoBehaviour _coroutines;

        public bool IsActive { get; private set; }

        public GameSession(SceneLoader scenes, MonoBehaviour coroutineHost)
        {
            _scenes = scenes;
            _coroutines = coroutineHost;
        }

        public void Start(Action onReady)
        {
            if (IsActive)
            {
                onReady?.Invoke();
                return;
            }

            _coroutines.StartCoroutine(StartRoutine(onReady));
        }

        public void Dispose(Action onDone = null)
        {
            _coroutines.StartCoroutine(DisposeRoutine(onDone));
        }

        IEnumerator StartRoutine(Action onReady)
        {
            yield return _scenes.LoadGameAdditive();
            IsActive = _scenes.IsGameLoaded;
            if (!IsActive)
            {
                GameLog.Error("GameSession.Start failed — Game scene not loaded.");
                yield break;
            }

            // Drop menu after Game is up so IShellUi is not used by Playing/Paused.
            yield return _scenes.UnloadMainMenu();
            onReady?.Invoke();
        }

        IEnumerator DisposeRoutine(Action onDone)
        {
            yield return _scenes.UnloadGame();
            yield return _scenes.LoadMainMenuAdditive();
            IsActive = false;
            onDone?.Invoke();
        }
    }
}
