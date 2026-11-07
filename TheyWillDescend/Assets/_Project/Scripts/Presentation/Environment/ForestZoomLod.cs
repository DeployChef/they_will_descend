using System.Collections.Generic;
using TheyWillDescend.Presentation.Cameras;
using UnityEngine;

namespace TheyWillDescend.Presentation.Environment
{
    /// <summary>
    /// Zoom-dependent foliage groups and fixed low-detail decorative forest.
    /// Pan, yaw and distance to individual trees never participate in selection.
    /// Far-background forest is deliberately not collected from the entire scene.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ForestZoomLod : MonoBehaviour
    {
        [SerializeField] RTSCameraController cameraController;

        [SerializeField, Tooltip("Необязательно: дальний LOD при переходе на камеру меню. Без ссылки поведение прежнее.")]
        PauseCameraSwitch menuCameraSwitch;

        [SerializeField, Range(.05f, .95f), Tooltip("Вес камеры меню, после которого используется дальний LOD, включая обратный переход.")]
        float menuLodBlendThreshold = .5f;

        [SerializeField, Tooltip("Группы с переключением: ближе границы зума — первый LOD, дальше — дальний (до LOD2).")]
        Transform[] treeRoots = System.Array.Empty<Transform>();

        [SerializeField, Tooltip("Внешний лес за лавой: всегда LOD2. Имеет приоритет над переключаемыми группами.")]
        Transform[] fixedLod2Roots = System.Array.Empty<Transform>();

        [SerializeField, Min(.01f), Tooltip("Граница радиуса RTS-зума. Меньше — первый LOD, больше — дальний (до LOD2).")]
        float detailSwitchRadius = 6.5f;

        [SerializeField, Min(0f), Tooltip("Запас вокруг границы, исключающий дрожание выбора уровня.")]
        float hysteresis = .15f;

        readonly List<Entry> _entries = new();
        bool _refreshRequested = true;
        int _currentLod = -1;

        public int CurrentLod => menuCameraSwitch && menuCameraSwitch.MenuBlendProgress >= menuLodBlendThreshold
            ? 2 : _currentLod;

        sealed class Entry
        {
            public LODGroup Group;
            public int FarLod;
            public bool FixedLod2;
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
            menuLodBlendThreshold = Mathf.Clamp(menuLodBlendThreshold, .05f, .95f);
            // OnValidate can run during deserialization; defer all renderer writes.
            _refreshRequested = true;
        }

        void OnTransformChildrenChanged() => _refreshRequested = true;

        void RequestRefresh() => _refreshRequested = true;

        void LateUpdate()
        {
            if (_refreshRequested) Refresh();
            if (cameraController)
            {
                var radius = cameraController.CurrentZoomRadius;
                // Fixed forest does not need to wait for the camera's Start.
                if (radius > 0f)
                    _currentLod = _currentLod < 0
                        ? radius <= detailSwitchRadius ? 0 : 2
                        : _currentLod == 0
                            ? radius > detailSwitchRadius + hysteresis ? 2 : 0
                            : radius < detailSwitchRadius - hysteresis ? 0 : 2;
            }

            var currentLod = CurrentLod;
            foreach (var entry in _entries)
            {
                var group = entry.Group;
                if (!group || !group.enabled || !group.gameObject.activeInHierarchy)
                {
                    entry.AppliedLod = -1;
                    continue;
                }
                if (!entry.FixedLod2 && currentLod < 0) continue;
                var level = entry.FixedLod2 || currentLod != 0 ? entry.FarLod : 0;
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
            // Collect fixed roots first so overlapping assignments cannot enable LOD0.
            CollectGroups(fixedLod2Roots, true, seen);
            CollectGroups(treeRoots, false, seen);
            _refreshRequested = false;
        }

        void CollectGroups(Transform[] roots, bool fixedLod2, HashSet<LODGroup> seen)
        {
            if (roots == null) return;
            foreach (var root in roots)
            {
                if (!root) continue;
                foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
                {
                    if (!seen.Add(group) || group.lodCount == 0) continue;
                    _entries.Add(new Entry
                    {
                        Group = group,
                        FarLod = Mathf.Min(2, group.lodCount - 1),
                        FixedLod2 = fixedLod2
                    });
                }
            }
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
