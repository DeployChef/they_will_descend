using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using FMOD.Studio;
using FMODUnity;

namespace Futboloid.Main.Audio
{
    /// <summary>
    /// FMOD Audio Manager — основной менеджер звука на FMOD Unity Integration 2.03.11.
    /// </summary>
    public class FMODAudioManager : MonoBehaviour, IAudioManager
    {
        [Header("Config")]
        [SerializeField] private FMODAudioCatalog _catalog;

        [Header("Pool Settings")]
        [SerializeField] private int _sfxPoolSize = 8;
        [SerializeField] private int _uiPoolSize = 3;

        [Header("Fade Settings")]
        [SerializeField] private float _musicFadeDuration = 0.5f;
        [SerializeField] private float _pauseFadeDuration = 0.3f;

        private EventInstance _musicInstance;
        private EventInstance _pauseInstance;
        private readonly Dictionary<string, float> _cooldowns = new();
        private float _currentContextMusicVolume = 1f;

        private readonly UnityEvent<string> _onEventPlayed = new();

        public NavigationContext CurrentContext { get; private set; } = NavigationContext.None;

        public FMODAudioCatalog Catalog => _catalog;

        private void Awake()
        {
            if (_catalog == null)
            {
                Debug.LogError("[FMODAudioManager] AudioCatalog is not assigned!");
                enabled = false;
                return;
            }

            // Инициализация пулов
            InitializePools();
        }

        private void OnDestroy()
        {
            StopAll();
        }

        private void InitializePools()
        {
            // FMOD пулы настраиваются через Bus Max Voices в FMOD Studio.
            // Здесь мы сохраняем размеры для справки.
            Debug.Log($"[FMODAudioManager] SFX Pool: {_sfxPoolSize}, UI Pool: {_uiPoolSize}");
        }

        // --- IAudioManager ---

        public void Play(string eventPath, float pitch = 0, float pitchRandomRange = 0)
        {
            if (_catalog == null) return;

            if (!_catalog.TryGetDefinition(eventPath, out var definition))
            {
                Debug.LogWarning($"[FMODAudioManager] Sound not found in catalog: {eventPath}");
                return;
            }

            Play(definition, pitch, pitchRandomRange);
        }

        public void Play(FMODSoundDefinition definition, float pitch = 0, float pitchRandomRange = 0)
        {
            if (definition == null || string.IsNullOrEmpty(definition.EventPath)) return;

            // Проверка кулдауна
            if (!CheckCooldown(definition.EventPath, definition.Cooldown))
                return;

            // Применяем питч
            if (pitch <= 0) pitch = definition.Pitch;
            if (pitchRandomRange <= 0) pitchRandomRange = definition.PitchRandomRange;
            pitch += UnityEngine.Random.Range(-pitchRandomRange, pitchRandomRange);

            // Воспроизведение через RuntimeManager
            var instance = RuntimeManager.CreateInstance(definition.EventPath);
            if (instance == null)
            {
                Debug.LogError($"[FMODAudioManager] Failed to create instance for: {definition.EventPath}");
                return;
            }

            instance.setPitch(pitch);
            instance.start();

            _onEventPlayed?.Invoke(definition.EventPath);

            // Для не-loop событий — авто-стоп
            if (!definition.Loop)
            {
                StartCoroutine(StopWhenFinished(instance, definition.FadeOutDuration));
            }
        }

        public void StopMusic(float fadeOutDuration = 0.5f)
        {
            if (_musicInstance == null) return;

            var duration = fadeOutDuration > 0 ? fadeOutDuration : _musicFadeDuration;
            StartCoroutine(FadeOutAndStop(_musicInstance, duration));
            _musicInstance = null;
        }

        public void PlayMusic(string eventPath, float fadeInDuration = 0.5f)
        {
            // Останавливаем текущую музыку
            if (_musicInstance != null)
            {
                _musicInstance.stop(StopMode.AllowFadeout);
                _musicInstance.release();
                _musicInstance = null;
            }

            var instance = RuntimeManager.CreateInstance(eventPath);
            if (instance == null)
            {
                Debug.LogError($"[FMODAudioManager] Failed to create music instance: {eventPath}");
                return;
            }

            // Громкость 0 для fade in
            instance.setVolume(0f);
            instance.start();

            _musicInstance = instance;
            _currentContextMusicVolume = 1f;

            if (fadeInDuration > 0)
            {
                StartCoroutine(FadeIn(instance, fadeInDuration));
            }
            else
            {
                instance.setVolume(1f);
            }
        }

        public void PauseMusic()
        {
            if (_musicInstance == null) return;

            var duration = _musicFadeDuration > 0 ? _musicFadeDuration : 0.3f;
            StartCoroutine(FadeOutAndPause(_musicInstance, duration));
        }

        public void UnPauseMusic()
        {
            if (_musicInstance == null) return;

            var duration = _musicFadeDuration > 0 ? _musicFadeDuration : 0.3f;
            StartCoroutine(FadeIn(_musicInstance, duration));
            _musicInstance.play();
        }

        public void PlayPauseSound(float fadeInDuration = 0.3f)
        {
            StopPauseSound(0f);

            var catalog = Catalog;
            if (catalog == null) return;

            var def = catalog.GetDefinition(FMODAudioCatalog.Paths.MusicPause);
            if (def == null) return;

            var instance = RuntimeManager.CreateInstance(def.EventPath);
            if (instance == null) return;

            instance.setVolume(0f);
            instance.setMode(MODE.LOOP_NORMAL);
            instance.start();

            _pauseInstance = instance;

            if (fadeInDuration > 0)
            {
                StartCoroutine(FadeIn(instance, fadeInDuration));
            }
            else
            {
                instance.setVolume(1f);
            }
        }

        public void StopPauseSound(float fadeOutDuration = 0.3f)
        {
            if (_pauseInstance == null) return;

            var duration = fadeOutDuration > 0 ? fadeOutDuration : _pauseFadeDuration;
            StartCoroutine(FadeOutAndStop(_pauseInstance, duration));
            _pauseInstance = null;
        }

        public void SetContext(NavigationContext context)
        {
            CurrentContext = context;
        }

        public void Dispose()
        {
            StopAll();
        }

        // --- Helpers ---

        private bool CheckCooldown(string eventPath, float cooldownSeconds)
        {
            if (cooldownSeconds <= 0f) return true;

            if (_cooldowns.TryGetValue(eventPath, out var lastTime))
            {
                if (Time.time - lastTime < cooldownSeconds)
                    return false;
            }

            _cooldowns[eventPath] = Time.time;
            return true;
        }

        private System.Collections.IEnumerator FadeOutAndStop(EventInstance instance, float duration)
        {
            float elapsed = 0f;
            float startVolume = 1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                instance.setVolume(Mathf.Lerp(startVolume, 0f, t));
                yield return null;
            }

            instance.setVolume(0f);
            instance.stop(StopMode.AllowFadeout);
            instance.release();
        }

        private System.Collections.IEnumerator FadeOutAndPause(EventInstance instance, float duration)
        {
            float elapsed = 0f;
            float startVolume = 1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                instance.setVolume(Mathf.Lerp(startVolume, 0f, t));
                yield return null;
            }

            instance.setVolume(0f);
            instance.pause();
        }

        private System.Collections.IEnumerator FadeIn(EventInstance instance, float duration)
        {
            float elapsed = 0f;
            float startVolume = 0f;
            float targetVolume = 1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                instance.setVolume(Mathf.Lerp(startVolume, targetVolume, t));
                yield return null;
            }

            instance.setVolume(targetVolume);
        }

        private System.Collections.IEnumerator StopWhenFinished(EventInstance instance, float fadeOutDuration)
        {
            PLAYBACK_STATE state;
            instance.getPlaybackState(out state);

            while (state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING)
            {
                instance.getPlaybackState(out state);
                yield return null;
            }

            if (fadeOutDuration > 0f)
            {
                float elapsed = 0f;
                float startVolume = 1f;

                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                    instance.setVolume(Mathf.Lerp(startVolume, 0f, t));
                    yield return null;
                }

                instance.setVolume(0f);
            }

            instance.stop(StopMode.AllowFadeout);
            instance.release();
        }

        private void StopAll()
        {
            if (_musicInstance != null)
            {
                _musicInstance.stop(StopMode.IMMEDIATE);
                _musicInstance.release();
                _musicInstance = null;
            }

            if (_pauseInstance != null)
            {
                _pauseInstance.stop(StopMode.IMMEDIATE);
                _pauseInstance.release();
                _pauseInstance = null;
            }

            _cooldowns.Clear();
        }

        // --- Events ---

        public UnityEvent<string> OnEventPlayed => _onEventPlayed;
    }
}
