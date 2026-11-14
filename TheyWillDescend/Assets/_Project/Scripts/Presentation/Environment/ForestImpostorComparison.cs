using System.Collections.Generic;
using UnityEngine;

namespace TheyWillDescend.Presentation.Environment
{
    [DisallowMultipleComponent]
    public sealed class ForestImpostorComparison : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField] GameObject[] originalTreeGroups;
        [SerializeField] GameObject[] impostorGroups;

        List<(GameObject group, bool wasActive)> savedStates;

        [ContextMenu("Compare/1. Our 3D trees")]
        void ShowOriginalTrees()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || savedStates != null) return;

            savedStates = new List<(GameObject, bool)>();
            SetGroups(impostorGroups, false);
            SetGroups(originalTreeGroups, true);
        }

        [ContextMenu("Compare/2. New Tree 01 impostors")]
        void ShowNineCardImpostors()
        {
            if (Application.isPlaying) Restore();
        }

        void Restore()
        {
            if (savedStates == null) return;
            foreach (var (group, wasActive) in savedStates)
                if (group) group.SetActive(wasActive);
            savedStates = null;
        }

        void OnDisable() => Restore();

        void SetGroups(GameObject[] groups, bool active)
        {
            foreach (var group in groups)
            {
                if (!group) continue;
                savedStates.Add((group, group.activeSelf));
                group.SetActive(active);
            }
        }
#endif
    }
}
