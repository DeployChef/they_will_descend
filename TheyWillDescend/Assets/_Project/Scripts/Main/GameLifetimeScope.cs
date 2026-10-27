using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Shell;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TheyWillDescend.Main
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [Header("Editor only")]
        [SerializeField, Tooltip("Run started when Game is opened directly in the editor. The menu's choice wins; builds always start Normal.")]
        EditorRunLaunch editorLaunch = EditorRunLaunch.Normal;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<GameSession>();
            builder.RegisterComponentInHierarchy<PauseMenuScreen>();
            builder.RegisterEntryPoint<GameEntry>().WithParameter(editorLaunch);
            builder.RegisterEntryPoint<SimPauseAudioBridge>();

            builder.RegisterComponentInHierarchy<RTSCameraController>();
            builder.RegisterComponentInHierarchy<AudioZoneManager>();
            builder.RegisterComponentInHierarchy<AudioVisibilityChecker>();
            builder.RegisterComponentInHierarchy<ZoneAmbienceSfxScheduler>();
            builder.RegisterComponentInHierarchy<WindAmbience>();
            builder.RegisterComponentInHierarchy<BuildingViewBoard>();
        }
    }
}
