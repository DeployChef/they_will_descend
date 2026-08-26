using System;
using System.Collections;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Io;
using UnityEngine;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// One gameplay run: show Loading, load Game, wait until simulation exists, then hide Loading.
    /// Menu UI port dies with MainMenu — Playing must not use <see cref="ShellUiPort"/>.
    /// After <see cref="Dispose"/>, MainMenu is back; caller should TransitionTo MainMenu.
    /// </summary>
    public sealed class GameSession
    {
        const float SimulationReadyTimeoutSeconds = 30f;

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
            yield return _scenes.LoadLoadingAdditive();
            yield return _scenes.UnloadMainMenu();
            yield return _scenes.LoadGameAdditive();
            IsActive = _scenes.IsGameLoaded;
            if (!IsActive)
            {
                GameLog.Error("GameSession.Start failed — Game scene not loaded.");
                yield break;
            }

            yield return WaitUntilSimulationReady();
            yield return _scenes.UnloadLoading();
            onReady?.Invoke();
        }

        IEnumerator DisposeRoutine(Action onDone)
        {
            yield return _scenes.UnloadLoading();
            yield return _scenes.UnloadGame();
            yield return _scenes.LoadMainMenuAdditive();
            yield return null;
            IsActive = false;
            onDone?.Invoke();
        }

        static IEnumerator WaitUntilSimulationReady()
        {
            var t0 = Time.realtimeSinceStartup;
            while (!SimWorld.TryGet(out _, out _))
            {
                if (Time.realtimeSinceStartup - t0 > SimulationReadyTimeoutSeconds)
                {
                    GameLog.Error(
                        "GameSession: SimControl never appeared (SubScene bake). " +
                        "Check Simulation SubScene is in Game and in Play Mode.");
                    yield break;
                }

                yield return null;
            }
        }
    }
}
