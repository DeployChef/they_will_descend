using TheyWillDescend.Presentation.ShellUi;
using VContainer;
using VContainer.Unity;

namespace TheyWillDescend.Main
{
    public sealed class MainMenuLifetimeScope : SceneLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<MainMenuScreen>();
            builder.RegisterComponentInHierarchy<MainMenuFlow>();
        }
    }
}
