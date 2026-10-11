using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Shell;
using VContainer;
using VContainer.Unity;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// Parent container on Root. Does not build itself: Auto Run is off on this component.
    /// <see cref="Startup"/> calls <see cref="LifetimeScope.Build"/> from Awake.
    /// </summary>
    public sealed class RootLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<AppContext>(Lifetime.Singleton);
            builder.Register<ShellService>(Lifetime.Singleton);
            builder.Register<SaveService>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<GameAudio>();
            builder.RegisterComponentInHierarchy<GameInput>();
        }
    }
}
