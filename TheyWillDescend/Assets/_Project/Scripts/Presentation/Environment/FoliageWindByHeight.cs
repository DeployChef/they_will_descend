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

        readonly List<Entry> _entries = new();
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
            if (!windMaterial || !staticMaterial || windMaterial == staticMaterial)
                return;
            if (windMaterial.shader != staticMaterial.shader)
            {
                Debug.LogWarning("Foliage wind/static materials must use the same shader. Materials are unchanged.", this);
                return;
            }

            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                // This group owns only these two shared materials, never unrelated art.
                if (renderer.sharedMaterials.Length != 1 || !IsManaged(renderer.sharedMaterial)
                    || !renderer.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh)
                    continue;
                var mesh = filter.sharedMesh;
                if (!TryGetGeometryBounds(mesh, out var bounds))
                    continue;

                var entry = new Entry
                {
                    Renderer = renderer,
                    Filter = filter,
                    Mesh = mesh,
                    GeometryBounds = bounds
                };
                _entries.Add(entry);
                Apply(entry, filter.transform.localToWorldMatrix);
            }
        }

        bool IsManaged(Material material) => material == windMaterial || material == staticMaterial;

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
            var target = height <= maxHeightWithoutWind ? staticMaterial : windMaterial;
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
                if (!entry.Renderer || !entry.Filter || entry.Filter.sharedMesh != entry.Mesh)
                {
                    _refreshRequested = true;
                    continue;
                }
                if (!IsManaged(entry.Renderer.sharedMaterial))
                    continue;
                var matrix = entry.Filter.transform.localToWorldMatrix;
                if (entry.LastMatrix != matrix)
                    Apply(entry, matrix);
            }
        }
#endif
    }
}
