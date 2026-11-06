using System.Collections.Generic;
using TheyWillDescend.Presentation.Cameras;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheyWillDescend.Presentation.Environment
{
    /// <summary>
    /// Applies a shadow-casting policy to foliage under this group using its resting world height.
    /// Zoom gates casting for the entire group; runtime renderer writes happen on transitions.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FoliageShadowsByHeight : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        [Tooltip("Кусты не выше этого значения (в метрах) не отбрасывают тени.")]
        float maxHeightWithoutShadows = 1.5f;

        [SerializeField]
        [Tooltip("Режим отбрасывания теней для кустов выше порога.")]
        ShadowCastingMode tallPlantShadows = ShadowCastingMode.TwoSided;

        [Header("Camera Zoom")]
        [SerializeField] RTSCameraController cameraController;

        [SerializeField, Min(0f)]
        [Tooltip("После этого радиуса RTS-зумa тени всех кустов отключаются.")]
        float shadowSwitchRadius = 10f;

        [SerializeField, Min(0f)]
        [Tooltip("Запас у порога зума, чтобы тени не переключались туда-обратно.")]
        float zoomHysteresis = 0.25f;

        readonly List<Entry> _entries = new();
        readonly Dictionary<Mesh, Bounds> _geometryBounds = new();
        readonly HashSet<Mesh> _unreadableMeshes = new();
        bool _refreshRequested;
        bool _zoomStateInitialized;
        bool _zoomAllowsShadows = true;
#if UNITY_EDITOR
        double _nextEditorCheck;
#endif

        public float MaxHeightWithoutShadows => maxHeightWithoutShadows;

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
            _zoomStateInitialized = false;
            _zoomAllowsShadows = true;
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
            maxHeightWithoutShadows = Mathf.Max(0f, maxHeightWithoutShadows);
            shadowSwitchRadius = Mathf.Max(0f, shadowSwitchRadius);
            zoomHysteresis = Mathf.Clamp(zoomHysteresis, 0f, shadowSwitchRadius);
            _refreshRequested = true;
        }

        void OnTransformChildrenChanged() => _refreshRequested = true;

        void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject))
                return;

            if (_refreshRequested)
                Refresh();

            var allowsShadows = true;
            if (cameraController != null)
            {
                var radius = cameraController.CurrentZoomRadius;
                allowsShadows = !_zoomStateInitialized
                    ? radius <= shadowSwitchRadius
                    : _zoomAllowsShadows
                        ? radius <= shadowSwitchRadius + zoomHysteresis
                        : radius < shadowSwitchRadius - zoomHysteresis;
            }

            if (_zoomStateInitialized && allowsShadows == _zoomAllowsShadows)
                return;

            _zoomStateInitialized = true;
            _zoomAllowsShadows = allowsShadows;
            foreach (var entry in _entries)
            {
                if (entry.Renderer && entry.Filter)
                    Apply(entry, entry.Filter.transform.localToWorldMatrix);
                else
                    _refreshRequested = true;
            }
        }

        /// <summary>Call after changing foliage size or hierarchy at runtime.</summary>
        [ContextMenu("Refresh Shadow Casting")]
        public void Refresh()
        {
            _entries.Clear();
            _geometryBounds.Clear();
            _unreadableMeshes.Clear();

            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh)
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

            _refreshRequested = false;
        }

        bool TryGetGeometryBounds(Mesh mesh, out Bounds bounds)
        {
            if (_geometryBounds.TryGetValue(mesh, out bounds))
                return true;

            // The current grass assets are readable. Do not use their wind-padded
            // mesh.bounds as a size substitute if a future asset is not readable.
            if (!mesh.isReadable)
            {
                if (_unreadableMeshes.Add(mesh))
                    Debug.LogWarning($"Cannot measure foliage mesh '{mesh.name}': Read/Write is disabled. Shadow casting is unchanged.", this);
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
            // Project the resting geometry bounds onto world Y; this also handles
            // rotated foliage, nonuniform scaling, and scaled parents.
            var height = Mathf.Abs(matrix.m10) * size.x
                         + Mathf.Abs(matrix.m11) * size.y
                         + Mathf.Abs(matrix.m12) * size.z;
            var mode = !_zoomAllowsShadows || height <= maxHeightWithoutShadows
                ? ShadowCastingMode.Off
                : tallPlantShadows;

            if (entry.Renderer.shadowCastingMode != mode)
            {
                entry.Renderer.shadowCastingMode = mode;
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
        // This editor-only check also catches changes to ancestor transforms.
        // It performs no repeated mesh reads and does not run in a player.
        void UpdateEditor()
        {
            if (!this || !isActiveAndEnabled || Application.IsPlaying(gameObject))
                return;

            var now = UnityEditor.EditorApplication.timeSinceStartup;
            if (!_refreshRequested && now < _nextEditorCheck)
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

                var matrix = entry.Filter.transform.localToWorldMatrix;
                if (!matrix.Equals(entry.LastMatrix))
                    Apply(entry, matrix);
            }
        }
#endif
    }
}
