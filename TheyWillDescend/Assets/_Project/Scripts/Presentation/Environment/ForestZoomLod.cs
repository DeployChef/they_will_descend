using System.Collections.Generic;
using TheyWillDescend.Presentation.Cameras;
using UnityEngine;

namespace TheyWillDescend.Presentation.Environment
{
    /// <summary>
    /// One zoom policy for explicitly assigned plateau/front-line tree groups.
    /// Pan, yaw and distance to individual trees never participate in selection.
    /// Far-background forest is deliberately not collected from the entire scene.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ForestZoomLod : MonoBehaviour
    {
        [SerializeField] RTSCameraController cameraController;

        [SerializeField, Tooltip("Корни деревьев/групп на плато и первой линии за лавой. Дальний фон сюда не добавлять.")]
        Transform[] treeRoots = System.Array.Empty<Transform>();

        [SerializeField, Min(.01f), Tooltip("Граница между ближайшим шагом RTS-зума и следующим. Меньше — LOD0, больше — LOD2.")]
        float detailSwitchRadius = 6.5f;

        [SerializeField, Min(0f), Tooltip("Запас вокруг границы, исключающий дрожание выбора уровня.")]
        float hysteresis = .15f;

        readonly List<Entry> _entries = new();
        bool _refreshRequested = true;
        int _currentLod = -1;

        public int CurrentLod => _currentLod;

        sealed class Entry
        {
            public LODGroup Group;
            public int FarLod;
            public int AppliedLod = -1;
        }

        void OnEnable()
        {
            _currentLod = -1;
            _refreshRequested = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.hierarchyChanged += RequestRefresh;
#endif
        }

        void OnValidate()
        {
            detailSwitchRadius = Mathf.Max(.01f, detailSwitchRadius);
            hysteresis = Mathf.Clamp(hysteresis, 0f, detailSwitchRadius * .5f);
            // OnValidate can run during deserialization; defer all renderer writes.
            _refreshRequested = true;
        }

        void OnTransformChildrenChanged() => _refreshRequested = true;

        void RequestRefresh() => _refreshRequested = true;

        void LateUpdate()
        {
            if (_refreshRequested) Refresh();
            if (!cameraController) return;
            var radius = cameraController.CurrentZoomRadius;
            // The camera initializes its smoothed radius in Start.
            if (radius <= 0f) return;

            _currentLod = _currentLod < 0
                ? radius <= detailSwitchRadius ? 0 : 2
                : _currentLod == 0
                    ? radius > detailSwitchRadius + hysteresis ? 2 : 0
                    : radius < detailSwitchRadius - hysteresis ? 0 : 2;

            foreach (var entry in _entries)
            {
                var group = entry.Group;
                if (!group || !group.enabled || !group.gameObject.activeInHierarchy)
                {
                    entry.AppliedLod = -1;
                    continue;
                }
                var level = _currentLod == 0 ? 0 : entry.FarLod;
                if (entry.AppliedLod == level) continue;
                group.ForceLOD(level);
                entry.AppliedLod = level;
            }
        }

        /// <summary>Call after adding/removing trees at runtime or changing assigned roots.</summary>
        [ContextMenu("Refresh Forest LOD Groups")]
        public void Refresh()
        {
            ReleaseGroups();
            _entries.Clear();
            var seen = new HashSet<LODGroup>();
            foreach (var root in treeRoots)
            {
                if (!root) continue;
                foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
                {
                    if (!seen.Add(group) || group.lodCount == 0) continue;
                    _entries.Add(new Entry { Group = group, FarLod = Mathf.Min(2, group.lodCount - 1) });
                }
            }
            _refreshRequested = false;
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.hierarchyChanged -= RequestRefresh;
#endif
            ReleaseGroups();
            _entries.Clear();
            _currentLod = -1;
            _refreshRequested = true;
        }

        void ReleaseGroups()
        {
            foreach (var entry in _entries)
                // During scene teardown Unity has already deactivated child groups.
                // ForceLOD rejects inactive groups even if their enabled flag is true.
                if (entry.Group && entry.Group.enabled && entry.Group.gameObject.activeInHierarchy)
                    entry.Group.ForceLOD(-1);
        }
    }
}
