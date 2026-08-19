using VContainer;
using Futboloid.Core.Audio;
using Futboloid.Main.Audio;

namespace Futboloid.Core.Infrastructure
{
    /// <summary>
    /// DI-регистрации для Core (singleton, не зависит от сцены).
    /// </summary>
    public static class CoreScopeExtensions
    {
        public static IContainerBuilder RegisterAudio(this IContainerBuilder builder)
        {
            // Шина событий — singleton
            builder.Register<AudioEventBus>(Lifetime.Singleton);

            // Сервис аудио — singleton
            builder.Register<AudioService>(Lifetime.Singleton);

            return builder;
        }
    }
}
