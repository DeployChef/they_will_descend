using System;
using System.Collections.Generic;
using TheyWillDescend.Simulation.Content;
using UnityEngine;

namespace TheyWillDescend.Content
{
    /// <summary>
    /// One scene's look: the model under each catalog building and the people.
    /// Catalog prefabs stay the functional shell (stamp, view, widget, audio);
    /// a scene swaps the art by pointing its dressing at another set.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VisualSet",
        menuName = "They Will Descend/Visual Set")]
    public sealed class VisualSet : ScriptableObject
    {
        [Serializable]
        public struct BuildingLook
        {
            [Tooltip("Shell prefab from the building catalog.")]
            public GameObject Building;
            [Tooltip("Model placed under the shell. Its root transform is local to the shell.")]
            public GameObject Body;
        }

        [SerializeField] BuildingLook[] buildings = Array.Empty<BuildingLook>();
        [SerializeField] GameObject[] workers = Array.Empty<GameObject>();

        public IReadOnlyList<GameObject> Workers => workers;

        public GameObject FindBody(string typeId)
        {
            var id = ContentId.Normalize(typeId);
            if (buildings == null || string.IsNullOrEmpty(id))
                return null;
            for (var i = 0; i < buildings.Length; i++)
            {
                var look = buildings[i];
                if (look.Building == null)
                    continue;
                var stamp = look.Building.GetComponent<BuildingStamp>();
                if (stamp != null && stamp.TypeId == id)
                    return look.Body;
            }

            return null;
        }
    }
}
