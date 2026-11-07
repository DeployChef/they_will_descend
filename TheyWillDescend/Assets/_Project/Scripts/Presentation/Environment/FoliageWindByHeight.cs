using System.Collections.Generic;
using UnityEngine;

namespace TheyWillDescend.Presentation.Environment
{
    /// <summary>
    /// Selects shared static/wind materials from resting world height.
    /// Camera movement does not affect this policy. Call Refresh after runtime resizing.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FoliageWindByHeight : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        [Tooltip("Кусты не выше этого значения (в метрах) используют материал без ветра.")]
        float maxHeightWithoutWind = 1.5f;

        [SerializeField, Tooltip("Общий материал с включённым Wind Enabled.")]
        Material windMaterial;

        [SerializeField, Tooltip("Общий материал с выключенным Wind Enabled, на том же шейдере.")]
        Material staticMaterial;

        [System.Serializable]
        sealed class MaterialPair
        {
            [SerializeField] Material windMaterial;
            [SerializeField] Material staticMaterial;
            public Material Wind => windMaterial;
            public Material Static => staticMaterial;
        }

        [SerializeField, Tooltip("Дополнительные общие пары материалов для разных видов подлеска.")]
        MaterialPair[] additionalMaterialPairs = System.Array.Empty<MaterialPair>();

        readonly List<Entry> _entries = new();
        readonly Dictionary<Material, (Material Wind, Material Static)> _materialPairs = new();
        readonly Dictionary<Mesh, Bounds> _geometryBounds = new();
        readonly HashSet<Mesh> _unreadableMeshes = new();
        bool _refreshRequested;
#if UNITY_EDITOR
        double _nextEditorCheck;
#endif

        public float MaxHeightWithoutWind => maxHeightWithoutWind;

        sealed class Entry
        {
            public MeshRenderer Renderer;
            public MeshFilter Filter;
            public Mesh Mesh;
            public Bounds GeometryBounds;
            public Transform MeasurementSpace;
            public Matrix4x4 LastMatrix;
        }

        void OnEnable()
        {
            Refresh();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += UpdateEditor;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= UpdateEditor;
#endif
        }

        void OnValidate()
        {
            maxHeightWithoutWind = Mathf.Max(0f, maxHeightWithoutWind);
            // Defer renderer writes until the main-thread update.
            _refreshRequested = true;
        }

        void OnTransformChildrenChanged() => _refreshRequested = true;

        void LateUpdate()
        {
            if (Application.IsPlaying(gameObject) && _refreshRequested)
                Refresh();
        }

        /// <summary>Rebuild after runtime scale/mesh/hierarchy changes.</summary>
        [ContextMenu("Refresh Wind Materials")]
        public void Refresh()
        {
            _refreshRequested = false;
            _entries.Clear();
            _geometryBounds.Clear();
            _unreadableMeshes.Clear();
            _materialPairs.Clear();
            AddMaterialPair(windMaterial, staticMaterial);
            if (additionalMaterialPairs != null)
                foreach (var pair in additionalMaterialPairs)
                    if (pair != null) AddMaterialPair(pair.Wind, pair.Static);
            if (_materialPairs.Count == 0) return;

            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                // This group owns only explicitly assigned shared pairs.
                if (renderer.sharedMaterials.Length != 1 || !IsManaged(renderer.sharedMaterial)
                    || !renderer.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh)
                    continue;
                var mesh = filter.sharedMesh;
                var authored = FoliageRestBounds.TryGet(filter, out var bounds, out var measurementSpace);
                if (!authored && !TryGetGeometryBounds(mesh, out bounds))
                    continue;

                var entry = new Entry
                {
                    Renderer = renderer,
                    Filter = filter,
                    Mesh = mesh,
                    GeometryBounds = bounds,
                    MeasurementSpace = measurementSpace
                };
                _entries.Add(entry);
                Apply(entry, measurementSpace.localToWorldMatrix);
            }
        }

        void AddMaterialPair(Material wind, Material still)
        {
            if (!wind || !still || wind == still) return;
            if (wind.shader != still.shader)
            {
                Debug.LogWarning("Foliage wind/static materials must use the same shader. Pair ignored.", this);
                return;
            }
            if (_materialPairs.ContainsKey(wind) || _materialPairs.ContainsKey(still))
            {
                Debug.LogWarning("A foliage material belongs to more than one wind/static pair. Pair ignored.", this);
                return;
            }
            _materialPairs.Add(wind, (wind, still));
            _materialPairs.Add(still, (wind, still));
        }

        bool IsManaged(Material material) => material && _materialPairs.ContainsKey(material);

        bool TryGetGeometryBounds(Mesh mesh, out Bounds bounds)
        {
            if (_geometryBounds.TryGetValue(mesh, out bounds))
                return true;
            bounds = default;
            // Renderer/mesh bounds are padded for wind and overstate actual size.
            if (!mesh.isReadable)
            {
                if (_unreadableMeshes.Add(mesh))
                    Debug.LogWarning($"Cannot measure foliage mesh '{mesh.name}': Read/Write is disabled. Material is unchanged.", this);
                return false;
            }
            var vertices = mesh.vertices;
            if (vertices.Length == 0)
                return false;
            bounds = new Bounds(vertices[0], Vector3.zero);
            for (var i = 1; i < vertices.Length; i++)
                bounds.Encapsulate(vertices[i]);
            _geometryBounds.Add(mesh, bounds);
            return true;
        }

        void Apply(Entry entry, Matrix4x4 matrix)
        {
            var size = entry.GeometryBounds.size;
            // Resting world-space height, including parents and nonuniform scaling.
            var height = Mathf.Abs(matrix.m10) * size.x
                         + Mathf.Abs(matrix.m11) * size.y
                         + Mathf.Abs(matrix.m12) * size.z;
            if (!_materialPairs.TryGetValue(entry.Renderer.sharedMaterial, out var pair)) return;
            var target = height <= maxHeightWithoutWind ? pair.Static : pair.Wind;
            if (entry.Renderer.sharedMaterial != target)
            {
                entry.Renderer.sharedMaterial = target;
#if UNITY_EDITOR
                if (!Application.IsPlaying(gameObject))
                {
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(entry.Renderer);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(entry.Renderer.gameObject.scene);
                }
#endif
            }
            entry.LastMatrix = matrix;
        }

#if UNITY_EDITOR
        // Editor resizing updates automatically; no per-frame size polling in a build.
        void UpdateEditor()
        {
            if (!this || !isActiveAndEnabled || Application.IsPlaying(gameObject))
                return;
            var now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now < _nextEditorCheck)
                return;
            _nextEditorCheck = now + 0.25;
            if (_refreshRequested)
            {
                Refresh();
                return;
            }
            foreach (var entry in _entries)
            {
                if (!entry.Renderer || !entry.Filter || !entry.MeasurementSpace || entry.Filter.sharedMesh != entry.Mesh)
                {
                    _refreshRequested = true;
                    continue;
                }
                if (!IsManaged(entry.Renderer.sharedMaterial))
                    continue;
                var matrix = entry.MeasurementSpace.localToWorldMatrix;
                if (entry.LastMatrix != matrix)
                    Apply(entry, matrix);
            }
        }
#endif
    }
}
