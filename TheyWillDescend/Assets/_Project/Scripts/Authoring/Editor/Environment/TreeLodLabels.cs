using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>
    /// Optional Game View gizmo labels. Editor-only: no scene components, material
    /// changes or player-build code. Disable before performance captures.
    /// </summary>
    [InitializeOnLoad]
    public static class TreeLodLabels
    {
        const string MenuPath = "Tools/They Will Descend/Art/Tree LOD Labels";
        const string StateKey = "TWD.TreeLodLabels.Enabled";
        const string TreeShader = "TheyWillDescend/Environment/SG_TreeWind";
        static readonly Dictionary<LODGroup, GroupLabels> Cache = new Dictionary<LODGroup, GroupLabels>();
        static readonly MethodInfo SelectionMethod = typeof(LODUtility).GetMethod(
            "CalculateVisualizationData", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(Camera), typeof(LODGroup), typeof(int) }, null);
        static readonly FieldInfo ActiveLevel = SelectionMethod?.ReturnType.GetField("activeLODLevel");
        static readonly FieldInfo ActiveFade = SelectionMethod?.ReturnType.GetField("activeLODFade");
        static GUIStyle[] _styles;
        static bool _warned;

        public static bool Enabled => SessionState.GetBool(StateKey, false);

        static TreeLodLabels()
        {
            EditorApplication.hierarchyChanged += ClearCache;
            EditorApplication.projectChanged += ClearCache;
        }

        [MenuItem(MenuPath)]
        static void Toggle() => SetEnabled(!Enabled);

        [MenuItem(MenuPath, true)]
        static bool ValidateMenu()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        public static void SetEnabled(bool enabled)
        {
            SessionState.SetBool(StateKey, enabled);
            ClearCache();
            if (enabled)
            {
                // This is an Editor view preference, never a project/scene setting.
                foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    if (window.GetType().Name != "GameView") continue;
                    var property = window.GetType().GetProperty("drawGizmos",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (property != null && property.CanWrite) property.SetValue(window, true);
                }
            }
            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>Uses Unity's camera-specific selection, including lodBias and forced LOD.</summary>
        public static bool TryGetLevel(LODGroup group, Camera camera, out int level, out float fade)
        {
            level = -1;
            fade = 0;
            if (!group || !camera || SelectionMethod == null || ActiveLevel == null) return false;
            try
            {
                var info = SelectionMethod.Invoke(null, new object[] { camera, group, 0 });
                level = (int)ActiveLevel.GetValue(info);
                if (ActiveFade != null) fade = (float)ActiveFade.GetValue(info);
                return true;
            }
            catch (Exception exception)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("Tree LOD labels could not read Unity's LOD selection: " + exception.Message);
                }
                return false;
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void DrawLabel(LODGroup group, GizmoType gizmoType)
        {
            if (!Enabled || !group.enabled || !group.gameObject.activeInHierarchy) return;
            var camera = Camera.current;
            if (!camera || camera.cameraType != CameraType.Game || camera != Camera.main) return;
            if (!Cache.TryGetValue(group, out var data))
            {
                data = new GroupLabels(group);
                Cache.Add(group, data);
            }
            if (!data.IsTree) return;
            var anchor = group.transform.TransformPoint(group.localReferencePoint + Vector3.up * group.size * .58f);
            var point = camera.WorldToViewportPoint(anchor);
            if (point.z <= camera.nearClipPlane || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return;
            EnsureStyles();
            if (!TryGetLevel(group, camera, out var level, out var fade))
            {
                Handles.Label(anchor, "LOD ?", _styles[3]);
                return;
            }
            if (level < 0 || level >= data.Labels.Length)
            {
                Handles.Label(anchor, "CULLED", _styles[3]);
                return;
            }
            var transition = fade > .001f && fade < .999f;
            Handles.Label(anchor, transition ? data.FadeLabels[level] : data.Labels[level],
                _styles[Mathf.Min(level, 2)]);
        }

        static void ClearCache() => Cache.Clear();

        static void EnsureStyles()
        {
            if (_styles != null) return;
            var colors = new[] { new Color(.4f, 1f, .45f), new Color(1f, .82f, .25f),
                new Color(.35f, .85f, 1f), Color.white };
            _styles = new GUIStyle[colors.Length];
            for (var i = 0; i < colors.Length; i++)
            {
                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(7, 7, 4, 4)
                };
                style.normal.textColor = colors[i];
                style.normal.background = Texture2D.blackTexture;
                _styles[i] = style;
            }
        }

        sealed class GroupLabels
        {
            public readonly bool IsTree;
            public readonly string[] Labels;
            public readonly string[] FadeLabels;

            public GroupLabels(LODGroup group)
            {
                var lods = group.GetLODs();
                Labels = new string[lods.Length];
                FadeLabels = new string[lods.Length];
                for (var i = 0; i < lods.Length; i++)
                {
                    var triangles = 0;
                    foreach (var renderer in lods[i].renderers)
                    {
                        if (!renderer) continue;
                        foreach (var material in renderer.sharedMaterials)
                            if (material && material.shader && material.shader.name == TreeShader) IsTree = true;
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (!filter || !filter.sharedMesh) continue;
                        var mesh = filter.sharedMesh;
                        for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                            if (mesh.GetTopology(submesh) == MeshTopology.Triangles)
                                triangles += (int)mesh.GetIndexCount(submesh) / 3;
                    }
                    Labels[i] = "LOD" + i + " | " + triangles + " tri";
                    FadeLabels[i] = Labels[i] + " (fade)";
                }
            }
        }
    }
}
