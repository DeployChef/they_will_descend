using TheyWillDescend.Authoring.Scenario;
using TheyWillDescend.Content;
using UnityEditor;
using UnityEngine;

namespace TheyWillDescend.Authoring.Editor
{
    [CustomEditor(typeof(ScenarioBuildingPreview))]
    public sealed class ScenarioBuildingPreviewEditor : UnityEditor.Editor
    {
        void OnSceneGUI()
        {
            var e = Event.current;
            if (e.type != EventType.MouseUp || e.button != 0)
                return;
            if (Tools.current != Tool.Move)
                return;

            var preview = (ScenarioBuildingPreview)target;
            var authoring = preview.GetComponentInParent<ScenarioAuthoring>();
            if (authoring == null || !authoring.TryGetPlacement(out var config, out var center, out var catalog))
                return;
            if (!ScenarioAuthoringEditor.TryFootprint(catalog, preview.TypeId, out var footprint))
                return;

            var meshSize = ScenarioAuthoringEditor.MeshSize(catalog, preview.TypeId);
            Undo.RecordObject(preview, "Snap Scenario House");
            Undo.RecordObject(preview.transform, "Snap Scenario House");
            if (!preview.SnapFromWorld(config, center, footprint, meshSize))
                preview.ApplyPose(config, center, footprint, meshSize);

            EditorUtility.SetDirty(preview);
            ScenarioAuthoringEditor.Capture(authoring);
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (!EditorGUI.EndChangeCheck())
                return;

            var preview = (ScenarioBuildingPreview)target;
            var authoring = preview.GetComponentInParent<ScenarioAuthoring>();
            if (authoring == null || !authoring.TryGetPlacement(out var config, out var center, out var catalog))
                return;
            if (!ScenarioAuthoringEditor.TryFootprint(catalog, preview.TypeId, out var footprint))
                return;

            preview.ApplyPose(config, center, footprint, ScenarioAuthoringEditor.MeshSize(catalog, preview.TypeId));
            ScenarioAuthoringEditor.Capture(authoring);
        }
    }
}
