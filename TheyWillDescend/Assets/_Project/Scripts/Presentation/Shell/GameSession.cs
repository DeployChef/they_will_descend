using System;
using System.Collections;
using TheyWillDescend.Infrastructure.Logging;
using UnityEngine;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// One gameplay run: load Game, unload MainMenu. Menu UI port dies with that scene —
    /// Playing must not use <see cref="ShellUiPort"/>. After <see cref="Dispose"/>, MainMenu
    /// is back and the binder binds a fresh port; caller should TransitionTo MainMenu.
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

            yield return _scenes.UnloadMainMenu();
            onReady?.Invoke();
        }

        IEnumerator DisposeRoutine(Action onDone)
        {
            yield return _scenes.UnloadGame();
            yield return _scenes.LoadMainMenuAdditive();
            yield return null;
            IsActive = false;
            onDone?.Invoke();
        }
    }
}
