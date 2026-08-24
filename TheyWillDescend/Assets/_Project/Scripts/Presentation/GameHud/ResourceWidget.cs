using System.Collections.Generic;
using TheyWillDescend.Simulation.Io;
using TMPro;
using UnityEngine;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Pulls the session resource catalog into scene-authored chips. Does not build UI.
    /// Matches a chip by parent name (Wood / Food); unused chips hide.
    /// </summary>
    public sealed class ResourceWidget : MonoBehaviour
    {
        [SerializeField] TMP_Text resource1Label;
        [SerializeField] TMP_Text resource2Label;
        [SerializeField] TMP_Text resource3Label;
        [SerializeField] TMP_Text resource4Label;

        readonly List<ResourceView> _rows = new(8);

        void Update()
        {
            if (SimIo.CopyResourceLedger(_rows) == 0)
                return;

            var labels = new[] { resource1Label, resource2Label, resource3Label, resource4Label };
            var used = new bool[labels.Length];
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var slot = IndexOfName(labels, row.DisplayName, used);
                if (slot < 0)
                    slot = FirstFree(used);
                if (slot < 0)
                    continue;
                used[slot] = true;
                Paint(labels[slot], row);
            }

            for (var i = 0; i < labels.Length; i++)
            {
                if (!used[i])
                    SetChipVisible(labels[i], false);
            }
        }

        static int IndexOfName(TMP_Text[] labels, string displayName, bool[] used)
        {
            if (string.IsNullOrEmpty(displayName))
                return -1;
            for (var i = 0; i < labels.Length; i++)
            {
                if (used[i] || labels[i] == null)
                    continue;
                var parent = labels[i].transform.parent;
                if (parent != null && string.Equals(parent.name, displayName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        static int FirstFree(bool[] used)
        {
            for (var i = 0; i < used.Length; i++)
            {
                if (!used[i])
                    return i;
            }

            return -1;
        }

        static void Paint(TMP_Text valueLabel, in ResourceView row)
        {
            if (valueLabel == null)
                return;
            SetChipVisible(valueLabel, true);
            valueLabel.text = Mathf.FloorToInt(row.Amount).ToString();
            var title = FindTitle(valueLabel);
            if (title != null)
                title.text = string.IsNullOrEmpty(row.DisplayName) ? row.ResourceId : row.DisplayName;
        }

        static TMP_Text FindTitle(TMP_Text valueLabel)
        {
            var parent = valueLabel.transform.parent;
            if (parent == null)
                return null;
            var titled = parent.Find("Title");
            if (titled != null)
            {
                var tmp = titled.GetComponent<TMP_Text>();
                if (tmp != null)
                    return tmp;
            }

            var tmps = parent.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < tmps.Length; i++)
            {
                if (tmps[i] != null && tmps[i] != valueLabel)
                    return tmps[i];
            }

            return null;
        }

        static void SetChipVisible(TMP_Text valueLabel, bool visible)
        {
            if (valueLabel == null)
                return;
            var root = valueLabel.transform.parent != null
                ? valueLabel.transform.parent.gameObject
                : valueLabel.gameObject;
            if (root.activeSelf != visible)
                root.SetActive(visible);
        }
    }
}
