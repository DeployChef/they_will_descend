using TheyWillDescend.App;
using TheyWillDescend.Infrastructure.Save;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// One-slot save/load. Closes build overlay first; does not own the clock.
    /// </summary>
    public sealed class SaveWidget : MonoBehaviour
    {
        [SerializeField] Button saveButton;
        [SerializeField] Button loadButton;
        [SerializeField, FormerlySerializedAs("buildHud")] BuildWidget buildWidget;
        [SerializeField, FormerlySerializedAs("spawnHud")] SpawnWidget spawnWidget;

        void Awake()
        {
            HudButtons.Bind(saveButton, OnSaveClicked);
            HudButtons.Bind(loadButton, OnLoadClicked);
        }

        void OnDestroy()
        {
            HudButtons.Unbind(saveButton, OnSaveClicked);
            HudButtons.Unbind(loadButton, OnLoadClicked);
        }

        void OnSaveClicked()
        {
            EnsureWidgetRefs();
            buildWidget?.CloseIfBusy();

            var snapshot = RunSessionSnapshot.Capture();
            RunSnapshotStore.Write(snapshot);
        }

        void OnLoadClicked()
        {
            if (!RunSnapshotStore.TryRead(out var snapshot))
                return;

            EnsureWidgetRefs();
            buildWidget?.CloseIfBusy();

            RunSessionSnapshot.Apply(snapshot);
            spawnWidget?.PumpViews();
            buildWidget?.RebuildViews();
        }

        void EnsureWidgetRefs()
        {
            if (buildWidget == null)
                buildWidget = FindFirstObjectByType<BuildWidget>();
            if (spawnWidget == null)
                spawnWidget = FindFirstObjectByType<SpawnWidget>();
        }
    }
}
