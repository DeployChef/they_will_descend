using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>Edits the shared wood/leaf materials together to keep attachments coherent.</summary>
    public sealed class TreeWindControlsWindow : EditorWindow
    {
        static readonly string[] Properties =
        {
            "_WindStrength", "_WindSpeed", "_WindDirection", "_DirectionVariation",
            "_GustStrength", "_TreeSwaySpeed", "_TrunkBend", "_BranchSway",
            "_LeafFlutter", "_LeafFlutterSpeed", "_WindPhaseOffset"
        };
        static readonly string[] Labels =
        {
            "Wind strength", "Wind speed", "World direction (degrees)", "Direction variation",
            "Gust strength", "Tree sway speed", "Trunk bend (degrees)", "Branch sway (degrees)",
            "Leaf flutter (degrees)", "Leaf flutter speed", "Phase offset"
        };
        static readonly float[] Maximum = { 8, 12, 360, 120, 1, 6, 25, 20, 45, 20, 6.2831853f };
        GameObject _tree;

        [MenuItem("Tools/They Will Descend/Art/Tree Wind Controls")]
        static void Open()
        {
            var window = GetWindow<TreeWindControlsWindow>("Tree Wind");
            window._tree = Selection.activeGameObject;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Редактирует ОБЩИЕ материалы дерева одновременно: ствол, листва и тень. " +
                "Изменения видны у всех экземпляров с этими материалами. Для другой породы — копии обоих материалов. " +
                "Это настройка арта, не контроллер погоды.", MessageType.Info);
            _tree = (GameObject)EditorGUILayout.ObjectField("Tree / prefab", _tree, typeof(GameObject), true);
            if (!_tree) return;
            var materials = _tree.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials).Where(m => m &&
                    m.shader.name == "TheyWillDescend/Environment/SG_TreeWind").Distinct().ToArray();
            if (materials.Length == 0)
            {
                EditorGUILayout.HelpBox("В выбранном дереве нет материалов SG_TreeWind.", MessageType.Warning);
                return;
            }
            EditorGUILayout.LabelField("Shared materials", materials.Length.ToString());
            var enabled = materials[0].IsKeywordEnabled("_TREE_WIND_ENABLED");
            EditorGUI.BeginChangeCheck();
            enabled = EditorGUILayout.Toggle("Wind enabled", enabled);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(materials, "Toggle Tree Wind");
                foreach (var m in materials)
                {
                    if (enabled) m.EnableKeyword("_TREE_WIND_ENABLED"); else m.DisableKeyword("_TREE_WIND_ENABLED");
                    EditorUtility.SetDirty(m);
                }
            }
            for (var i = 0; i < Properties.Length; i++)
            {
                var value = materials[0].GetFloat(Properties[i]);
                EditorGUI.showMixedValue = materials.Any(m => !Mathf.Approximately(m.GetFloat(Properties[i]), value));
                EditorGUI.BeginChangeCheck();
                var next = EditorGUILayout.Slider(Labels[i], value, 0, Maximum[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(materials, "Adjust Tree Wind");
                    foreach (var m in materials) { m.SetFloat(Properties[i], next); EditorUtility.SetDirty(m); }
                    SceneView.RepaintAll();
                }
                EditorGUI.showMixedValue = false;
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Gentle")) Preset(materials, .65f, .7f, .4f, .65f, 9f);
            if (GUILayout.Button("Wind")) Preset(materials, 1f, 1f, .6f, .8f, 14f);
            if (GUILayout.Button("Storm")) Preset(materials, 3.5f, 2f, 1f, 1.1f, 32f);
            EditorGUILayout.EndHorizontal();
        }

        static void Preset(Material[] materials, float strength, float speed, float gust, float swaySpeed, float variation)
        {
            Undo.RecordObjects(materials, "Tree Wind Preset");
            foreach (var m in materials)
            {
                m.EnableKeyword("_TREE_WIND_ENABLED");
                m.SetFloat("_WindStrength", strength); m.SetFloat("_WindSpeed", speed);
                m.SetFloat("_GustStrength", gust); m.SetFloat("_TreeSwaySpeed", swaySpeed);
                m.SetFloat("_DirectionVariation", variation); m.SetFloat("_WindPreviewTime", -1f);
                EditorUtility.SetDirty(m);
            }
            SceneView.RepaintAll();
        }
    }
}
