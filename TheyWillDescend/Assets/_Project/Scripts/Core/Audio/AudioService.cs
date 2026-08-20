using System;
using UnityEngine;

namespace Futboloid.Core.Audio
{
    /// <summary>
    /// Сервис маппинга игровых событий в FMOD Event Path.
    /// Слушает шину событий и вызывает IAudioManager.Play().
    /// </summary>
    public class AudioService : IDisposable
    {
        private readonly IAudioManager _audioManager;
        private readonly AudioEventBus _eventBus;

        public AudioService(IAudioManager audioManager, AudioEventBus eventBus)
        {
            _audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            Subscribe();
        }

        private void Subscribe()
        {
            _eventBus.Subscribe(OnEvent);
        }

        private void OnEvent(GameEvent e)
        {
            switch (e)
            {
                case NavigationChangedEvent nav:
                    HandleNavigationChanged(nav);
                    break;
                case MatchStartedEvent _:
                    HandleMatchStarted();
                    break;
                case MatchEndedEvent _:
                    HandleMatchEnded();
                    break;
                case PitchResetRequestedEvent reset:
                    HandlePitchReset(reset);
                    break;
            }
        }

        private void HandleNavigationChanged(NavigationChangedEvent e)
        {
            _audioManager.SetContext(e.NewContext);

            switch (e.NewContext)
            {
                case NavigationContext.OnField:
                    // Возврат на поле — продолжаем музыку
                    if (_audioManager.CurrentContext == NavigationContext.Paused)
                    {
                        _audioManager.UnPauseMusic();
                    }
                    else
                    {
                        _audioManager.PlayMusic(FMODAudioCatalog.Paths.MusicMatch);
                    }
                    break;

                case NavigationContext.Paused:
                    // Переход в паузу
                    _audioManager.PauseMusic();
                    _audioManager.PlayPauseSound();
                    break;

                case NavigationContext.MainMenu:
                    // Переход в главное меню
                    _audioManager.PauseMusic();
                    _audioManager.PlayPauseSound();
                    break;

                case NavigationContext.Tournament:
                    // Турнирное меню
                    _audioManager.Play(FMODAudioCatalog.Paths.UiTournamentOpen);
                    break;
            }
        }

        private void HandleMatchStarted()
        {
            // Заглушка: только свисток, без музыки
            _audioManager.Play(FMODAudioCatalog.Paths.MatchStart);
        }

        private void HandleMatchEnded()
        {
            _audioManager.Play(FMODAudioCatalog.Paths.MatchEnd);
            _audioManager.StopMusic();
        }

        private void HandlePitchReset(PitchResetRequestedEvent e)
        {
            if (e.IsOnField)
            {
                // Рестарт турнира на поле
                _audioManager.StopMusic();
                _audioManager.StopPauseSound();
                _audioManager.PlayMusic(FMODAudioCatalog.Paths.MusicMatch);
            }
            else
            {
                // Не на поле — только стоп музыки, старт при следующем OnField
                _audioManager.StopMusic();
            }
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe(OnEvent);
        }
    }
}
