using UnityEngine;
using Futboloid.Core.Audio;

namespace Futboloid.Main.Audio
{
    /// <summary>
    /// Заглушка для тестирования FMOD аудио.
    /// Публикует MatchStartedEvent при нажатии пробела.
    /// </summary>
    public class AudioTestStub : MonoBehaviour
    {
        [SerializeField] private AudioEventBus _eventBus;

        private void Awake()
        {
            if (_eventBus == null)
            {
                Debug.LogError("[AudioTestStub] EventBus not assigned!");
                enabled = false;
            }
        }

        private void Update()
        {
            // Пробел — эмулирует начало матча
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("[AudioTestStub] Publishing MatchStartedEvent");
                _eventBus.Publish(new MatchStartedEvent());
            }

            // N — прямое воспроизведение свистка
            if (Input.GetKeyDown(KeyCode.N))
            {
                if (TryGetComponent(out FMODAudioManager manager))
                {
                    manager.Play(FMODAudioCatalog.Paths.MatchStart);
                }
            }

            // M — начало музыки матча
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (TryGetComponent(out FMODAudioManager manager))
                {
                    manager.SetContext(NavigationContext.OnField);
                    manager.PlayMusic(FMODAudioCatalog.Paths.MusicMatch);
                }
            }

            // P — пауза
            if (Input.GetKeyDown(KeyCode.P))
            {
                if (TryGetComponent(out FMODAudioManager manager))
                {
                    manager.SetContext(NavigationContext.Paused);
                    _eventBus.Publish(new NavigationChangedEvent(
                        NavigationContext.Paused,
                        NavigationContext.OnField));
                }
            }
        }
    }
}
