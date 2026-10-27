using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Shell;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TheyWillDescend.Main
{
    /// <summary>
    /// Process scope. VContainerSettings instantiates this prefab once, marks it DontDestroyOnLoad,
    /// and makes it the parent of every scene LifetimeScope. Nothing here may depend on a scene.
    /// </summary>
    public sealed class AppScope : LifetimeScope
    {
        [SerializeField] GameInput gameInput;
        [SerializeField] LoadingScreen loadingScreen;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<ShellService>(Lifetime.Singleton);
            builder.Register<TransitionService>(Lifetime.Singleton);
            builder.Register<LoadingTasks>(Lifetime.Singleton);
            builder.Register<SaveService>(Lifetime.Singleton);
            builder.RegisterComponent(gameInput);
            builder.RegisterComponent(loadingScreen);
        }
    }
}
