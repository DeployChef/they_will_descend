using System;
using UnityEditor;
using UnityEngine;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>Explicit one-time preparation for future species; never executes in a build.</summary>
    public sealed class TreeWindAuthoringWindow : EditorWindow
    {
        GameObject _source;
        MeshRenderer[] _foliage = Array.Empty<MeshRenderer>();
        Material _bark;
        Material _leaves;
        string _folder = "Assets/_Project/Prefabs/Environment/Trees/NewTree_Wind";
        float _crownStart = .4f;
        float _flexibility = 1f;

        [MenuItem("Tools/They Will Descend/Art/Prepare Tree Wind")]
        static void Open() => GetWindow<TreeWindAuthoringWindow>("Prepare Tree Wind");

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Создаёт НОВЫЙ визуальный префаб и меши. Исходник сохраняется. " +
                "Root: Y вверх, основание у 0. Листва: COLOR R = от корня до кончика, G = фаза карточки. " +
                "UV1/UV2 займёт ветер. Игровые компоненты и LOD не копируются.", MessageType.Info);
            _source = (GameObject)EditorGUILayout.ObjectField("Source tree", _source, typeof(GameObject), true);
            var count = Mathf.Max(1, EditorGUILayout.IntField("Foliage renderers", _foliage.Length));
            if (count != _foliage.Length) Array.Resize(ref _foliage, count);
            for (var i = 0; i < _foliage.Length; i++)
                _foliage[i] = (MeshRenderer)EditorGUILayout.ObjectField("Leaves " + i, _foliage[i], typeof(MeshRenderer), true);
            _bark = (Material)EditorGUILayout.ObjectField("Wind bark material", _bark, typeof(Material), false);
            _leaves = (Material)EditorGUILayout.ObjectField("Wind leaf material", _leaves, typeof(Material), false);
            _crownStart = EditorGUILayout.Slider("Crown start / height", _crownStart, .05f, .9f);
            _flexibility = EditorGUILayout.Slider("Species flexibility", _flexibility, 0f, 2f);
            _folder = EditorGUILayout.TextField("New output folder", _folder);
            if (!GUILayout.Button("Create separate wind prefab")) return;
            try
            {
                var prefab = TreeWindMeshBaker.CreatePrefab(_source, _foliage, _bark, _leaves,
                    _folder, _crownStart, _flexibility);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
