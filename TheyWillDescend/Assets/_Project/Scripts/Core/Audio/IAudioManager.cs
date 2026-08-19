using System;

namespace Futboloid.Core.Audio
{
    /// <summary>
    /// Интерфейс аудиоменеджера. Реализация — FMODAudioManager.
    /// </summary>
    public interface IAudioManager : IDisposable
    {
        /// <summary>
        /// Воспроизвести FMOD-событие по Event Path.
        /// </summary>
        void Play(string eventPath, float pitch = 0, float pitchRandomRange = 0);

        /// <summary>
        /// Воспроизвести FMOD-событие по определению из каталога.
        /// </summary>
        void Play(FMODSoundDefinition definition, float pitch = 0, float pitchRandomRange = 0);

        /// <summary>
        /// Остановить воспроизведение (fade out).
        /// </summary>
        void StopMusic(float fadeOutDuration = 0.5f);

        /// <summary>
        /// Начать музыку (fade in).
        /// </summary>
        void PlayMusic(string eventPath, float fadeInDuration = 0.5f);

        /// <summary>
        /// Пауза музыки (сохраняет позицию).
        /// </summary>
        void PauseMusic();

        /// <summary>
        /// Снять музыку с паузы (continues from same position).
        /// </summary>
        void UnPauseMusic();

        /// <summary>
        /// Воспроизвести звук паузы (fade in).
        /// </summary>
        void PlayPauseSound(float fadeInDuration = 0.3f);

        /// <summary>
        /// Остановить звук паузы (fade out).
        /// </summary>
        void StopPauseSound(float fadeOutDuration = 0.3f);

        /// <summary>
        /// Текущий контекст навигации.
        /// </summary>
        NavigationContext CurrentContext { get; }

        /// <summary>
        /// Установить контекст навигации.
        /// </summary>
        void SetContext(NavigationContext context);
    }

    /// <summary>
    /// Контексты навигации.
    /// </summary>
    public enum NavigationContext
    {
        None,
        MainMenu,
        OnField,
        Paused,
        Tournament
    }
}
