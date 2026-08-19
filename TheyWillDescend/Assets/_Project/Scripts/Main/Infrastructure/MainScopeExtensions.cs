using VContainer;
using Futboloid.Core.Audio;
using Futboloid.Main.Audio;

namespace Futboloid.Infrastructure
{
    /// <summary>
    /// DI-регистрации для Main (зависит от сцены).
    /// </summary>
    public static class MainScopeExtensions
    {
        public static IContainerBuilder RegisterAudioManager(this IContainerBuilder builder)
        {
            // Менеджер звука — компонент сцены
            builder.RegisterComponentInHierarchy<FMODAudioManager>()
                .As<IAudioManager>()
                .AsSelf();

            return builder;
        }
    }
}
