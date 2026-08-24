using TheyWillDescend.Authoring.Scenario;
using TheyWillDescend.Simulation.City;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>
    /// Scene view paint for <see cref="ScenarioDefinition"/> buildings. Not a play-mode placement.
    /// </summary>
    [EditorTool("Place scenario buildings", typeof(ScenarioAuthoring))]
    public sealed class ScenarioPlaceEditorTool : EditorTool
    {
        string _typeId;

        static readonly Rect PaletteRect = new(12f, 12f, 320f, 92f);

        public override GUIContent toolbarIcon
        {
            get
            {
                var icon = EditorGUIUtility.IconContent("d_GridLayoutGroup Icon");
                return new GUIContent("Scn", icon.image, "Click the radial grid to place buildings into the scenario asset");
            }
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView)
                return;
            if (target is not ScenarioAuthoring authoring || authoring.Definition == null)
                return;
            if (!authoring.TryGetPlacement(out var config, out var center, out var catalog))
                return;

            DrawPalette(catalog);
            HandleClicks(authoring, config, center);
            DrawGhost(config, center, catalog);
        }

        void DrawPalette(TheyWillDescend.Authoring.City.BuildingCatalogAuthoring catalog)
        {
            var definitions = catalog.Catalog != null ? catalog.Catalog.Buildings : null;
            Handles.BeginGUI();
            GUILayout.BeginArea(PaletteRect, EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scenario place", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(_typeId)
                    ? "Pick a type, then click the grid. RMB removes."
                    : $"Click to place {_typeId}. RMB removes.");
            if (definitions != null)
            {
                EditorGUILayout.BeginHorizontal();
                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null)
                        continue;
                    var pressed = _typeId == definition.TypeId;
                    if (GUILayout.Toggle(pressed, definition.DisplayName, EditorStyles.miniButton) && !pressed)
                        _typeId = definition.TypeId;
                }

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        void HandleClicks(
            ScenarioAuthoring authoring,
            in RadialGridConfig config,
            float3 center)
        {
            var e = Event.current;
            if (PaletteRect.Contains(e.mousePosition))
                return;

            var id = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(id);

            if (e.type == EventType.MouseMove)
                SceneView.currentDrawingSceneView?.Repaint();

            if (e.alt || e.type != EventType.MouseDown)
                return;
            if (!TryPickOnPlane(center, out var world))
                return;

            if (e.button == 1)
            {
                TryRemoveAt(authoring, world);
                e.Use();
                return;
            }

            if (e.button != 0 || string.IsNullOrEmpty(_typeId))
                return;
            if (!RadialFootprintMath.TrySnapAnchor(center, config, world, out var cluster, out var radial))
                return;
            if (!ScenarioAuthoringEditor.TryPlaceAt(authoring, _typeId, cluster, radial))
                Debug.LogWarning($"Scenario place: cell ({cluster},{radial}) is occupied or invalid for {_typeId}.");
            e.Use();
        }

        void DrawGhost(
            in RadialGridConfig config,
            float3 center,
            TheyWillDescend.Authoring.City.BuildingCatalogAuthoring catalog)
        {
            if (string.IsNullOrEmpty(_typeId))
                return;
            if (!TryPickOnPlane(center, out var world))
                return;
            if (!RadialFootprintMath.TrySnapAnchor(center, config, world, out var cluster, out var radial))
                return;
            if (!ScenarioAuthoringEditor.TryFootprint(catalog, _typeId, out var footprint))
                return;

            RadialFootprintMath.FootprintMarkerPose(
                center, config, cluster, radial, footprint,
                out var position, out var rotation, out var size);
            var matrix = Matrix4x4.TRS(position, rotation, Vector3.one * size);
            using (new Handles.DrawingScope(new Color(0.2f, 0.85f, 0.4f, 0.85f), matrix))
                Handles.DrawWireCube(Vector3.zero, Vector3.one);
        }

        static void TryRemoveAt(ScenarioAuthoring authoring, float3 world)
        {
            var root = authoring.PreviewRoot;
            var closest = default(ScenarioBuildingPreview);
            var best = float.MaxValue;
            var previews = root.GetComponentsInChildren<ScenarioBuildingPreview>(true);
            for (var i = 0; i < previews.Length; i++)
            {
                var preview = previews[i];
                var delta = (float3)preview.transform.position - world;
                delta.y = 0f;
                var dist = math.lengthsq(delta);
                if (dist >= best || dist > 4f)
                    continue;
                best = dist;
                closest = preview;
            }

            if (closest == null)
                return;
            Undo.DestroyObjectImmediate(closest.gameObject);
            ScenarioAuthoringEditor.Capture(authoring);
        }

        static bool TryPickOnPlane(float3 center, out float3 world)
        {
            world = default;
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(center.x, center.y, center.z));
            if (!plane.Raycast(ray, out var enter))
                return false;
            world = ray.GetPoint(enter);
            return true;
        }
    }
}
