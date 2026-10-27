using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Shell;
using VContainer;
using VContainer.Unity;

namespace TheyWillDescend.Main
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<GameRun>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<GameSession>();
            builder.RegisterComponentInHierarchy<PauseMenuScreen>();
            builder.RegisterEntryPoint<GameEntry>();

            builder.RegisterComponentInHierarchy<RTSCameraController>();
            builder.RegisterComponentInHierarchy<AudioZoneManager>();
            builder.RegisterComponentInHierarchy<AudioVisibilityChecker>();
            builder.RegisterComponentInHierarchy<ZoneAmbienceSfxScheduler>();
            builder.RegisterComponentInHierarchy<BuildingViewBoard>();
        }
    }
}
