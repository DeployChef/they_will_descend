using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Shell;
using VContainer;
using VContainer.Unity;

namespace TheyWillDescend.Main
{
    public sealed class GameLifetimeScope : SceneLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<GameRun>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<GameSession>();
            builder.RegisterComponentInHierarchy<PauseMenuScreen>();
        }
    }
}
