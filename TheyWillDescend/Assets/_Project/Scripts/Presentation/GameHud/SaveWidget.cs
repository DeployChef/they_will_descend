using TheyWillDescend.App;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Presentation.Agents;
using TheyWillDescend.Presentation.City;
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
        [SerializeField] BuildingViewBoard buildingViewBoard;
        [SerializeField] AgentViewBoard agentViewBoard;

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
            buildWidget?.CloseIfBusy();

            var snapshot = RunSessionSnapshot.Capture();
            RunSnapshotStore.Write(snapshot);
        }

        void OnLoadClicked()
        {
            if (!RunSnapshotStore.TryRead(out var snapshot))
                return;

            buildWidget?.CloseIfBusy();

            RunSessionSnapshot.Apply(snapshot);
            agentViewBoard?.Pump();
            if (buildingViewBoard == null)
                GameLog.Error("SaveWidget: BuildingViewBoard is not assigned.");
            else
                buildingViewBoard.RebuildViews();
        }
    }
}
