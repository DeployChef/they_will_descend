using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] LoadingScreen loadingScreen;
        [SerializeField] AudioCatalog audioCatalog;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<ShellService>(Lifetime.Singleton);
            builder.Register<TransitionService>(Lifetime.Singleton);
            builder.Register<LoadingTasks>(Lifetime.Singleton);
            builder.Register<SaveService>(Lifetime.Singleton);
            builder.Register<InputService>(Lifetime.Singleton).WithParameter(inputActions);
            builder.RegisterComponent(loadingScreen);
            builder.RegisterInstance(audioCatalog);
            builder.RegisterEntryPoint<AudioService>().AsSelf();
        }
    }
}
