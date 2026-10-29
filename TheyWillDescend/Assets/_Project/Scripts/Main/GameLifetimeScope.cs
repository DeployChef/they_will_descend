using TheyWillDescend.Presentation.Agents;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Presentation.Cameras;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Presentation.GameHud;
using TheyWillDescend.Presentation.Environment;
using TheyWillDescend.Presentation.Shell;
using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Presentation.SimBridge;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TheyWillDescend.Main
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [Header("Editor only")]
        [SerializeField, Tooltip("Save id used when Game is opened directly in the editor. Empty starts a new run. The menu's choice wins. Builds ignore this.")]
        string editorSaveId;
        [SerializeField, Tooltip("New run uses the debug scenario when Game is opened directly in the editor. Ignored when a save id is set. Builds always start a normal new run.")]
        bool editorDebug = true;
        [SerializeField, Min(0f), Tooltip("Hold the loading curtain this many seconds per fake stage (five stages, then real work). 0 = off.")]
        float debugLoadStageSeconds;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<GameSession>();
            builder.RegisterComponentInHierarchy<PauseMenuScreen>();
            builder.RegisterEntryPoint<SimPauseAudioBridge>();

            builder.RegisterComponentInHierarchy<RTSCameraController>();
            builder.RegisterComponentInHierarchy<AudioZoneManager>();
            builder.RegisterComponentInHierarchy<AudioVisibilityChecker>();
            builder.RegisterComponentInHierarchy<ZoneAmbienceSfxScheduler>();
            builder.RegisterComponentInHierarchy<WindAmbience>();

            builder.RegisterComponentInHierarchy<SceneDressing>();
            builder.Register(r => r.Resolve<SceneDressing>().Visuals, Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<BuildingViewBoard>();
            builder.RegisterComponentInHierarchy<BuildPlacementController>();
            builder.RegisterComponentInHierarchy<BuildingInspectPanel>();
            builder.RegisterComponentInHierarchy<AgentViewBoard>();

            // After the scene container builds, VContainer calls GameEntry.Initialize — that is the run start.
            builder.RegisterEntryPoint<GameEntry>()
                .WithParameter(editorSaveId ?? "")
                .WithParameter(editorDebug)
                .WithParameter(debugLoadStageSeconds);
        }
    }
}
