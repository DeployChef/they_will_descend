using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheyWillDescend.Presentation.Environment
{
    /// <summary>Size-based casting for a rock group, independent of camera and rock variant.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class RockShadowsBySize : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Камни с самой длинной стороной не больше этого размера (метры) не отбрасывают тени.")]
        float maxSizeWithoutShadows = 2.5f;

        readonly List<Entry> _entries = new();
        bool _refreshRequested;
#if UNITY_EDITOR
        double _nextEditorCheck;
#endif

        sealed class Entry
        {
            public MeshRenderer Renderer;
            public MeshFilter Filter;
            public Mesh Mesh;
            public Matrix4x4 LastMatrix;
        }

        void OnEnable()
        {
            Refresh();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += UpdateEditor;
            UnityEditor.EditorApplication.hierarchyChanged += RequestRefresh;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= UpdateEditor;
            UnityEditor.EditorApplication.hierarchyChanged -= RequestRefresh;
#endif
        }

        void OnValidate()
        {
            maxSizeWithoutShadows = Mathf.Max(0f, maxSizeWithoutShadows);
            _refreshRequested = true;
        }

        void OnTransformChildrenChanged() => RequestRefresh();
        void RequestRefresh() => _refreshRequested = true;

        void LateUpdate()
        {
            if (_refreshRequested) Refresh();
        }

        /// <summary>Call after runtime scaling, mesh replacement or adding/removing rocks.</summary>
        [ContextMenu("Refresh Rock Shadows")]
        public void Refresh()
        {
            _entries.Clear();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh)
                    continue;
                var entry = new Entry { Renderer = renderer, Filter = filter, Mesh = filter.sharedMesh };
                _entries.Add(entry);
                Apply(entry);
            }
            _refreshRequested = false;
        }

        void Apply(Entry entry)
        {
            var matrix = entry.Filter.transform.localToWorldMatrix;
            var size = entry.Mesh.bounds.size;
            // Axis lengths preserve the decision under rotation and include scaled
            // parents, nonuniform scaling and shear. No CPU-readable mesh required.
            var longest = Mathf.Max(
                matrix.MultiplyVector(Vector3.right * size.x).magnitude,
                matrix.MultiplyVector(Vector3.up * size.y).magnitude,
                matrix.MultiplyVector(Vector3.forward * size.z).magnitude);
            var mode = longest <= maxSizeWithoutShadows ? ShadowCastingMode.Off : ShadowCastingMode.On;
            if (entry.Renderer.shadowCastingMode != mode)
            {
                entry.Renderer.shadowCastingMode = mode;
#if UNITY_EDITOR
                if (!Application.IsPlaying(gameObject))
                {
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(entry.Renderer);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
#endif
            }
            entry.LastMatrix = matrix;
        }

#if UNITY_EDITOR
        void UpdateEditor()
        {
            if (!this || !isActiveAndEnabled || Application.IsPlaying(gameObject)) return;
            var now = UnityEditor.EditorApplication.timeSinceStartup;
            if (!_refreshRequested && now < _nextEditorCheck) return;
            _nextEditorCheck = now + .25;
            if (_refreshRequested) { Refresh(); return; }
            foreach (var entry in _entries)
            {
                if (!entry.Renderer || !entry.Filter || entry.Filter.sharedMesh != entry.Mesh)
                {
                    _refreshRequested = true;
                    continue;
                }
                if (!entry.Filter.transform.localToWorldMatrix.Equals(entry.LastMatrix)) Apply(entry);
            }
        }
#endif
    }
}
