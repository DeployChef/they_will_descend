using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Io;
using TMPro;
using UnityEngine;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Pulls resource buffers into scene-authored chips. Does not build UI.
    /// Matches a chip by parent name (Wood / Food); unused chips hide.
    /// </summary>
    public sealed class ResourceWidget : MonoBehaviour
    {
        [SerializeField] TMP_Text resource1Label;
        [SerializeField] TMP_Text resource2Label;
        [SerializeField] TMP_Text resource3Label;
        [SerializeField] TMP_Text resource4Label;

        void Update()
        {
            if (!SimWorld.TryGet(out var em, out var bag)
                || !em.HasBuffer<ResourceAmount>(bag)
                || !em.HasBuffer<ResourceInfo>(bag))
                return;

            var stock = em.GetBuffer<ResourceAmount>(bag);
            var info = em.GetBuffer<ResourceInfo>(bag);
            if (info.Length == 0)
                return;

            var labels = new[] { resource1Label, resource2Label, resource3Label, resource4Label };
            var used = new bool[labels.Length];
            for (var i = 0; i < info.Length; i++)
            {
                var row = info[i];
                var displayName = row.DisplayName.ToString();
                var slot = IndexOfName(labels, displayName, used);
                if (slot < 0)
                    slot = FirstFree(used);
                if (slot < 0)
                    continue;
                used[slot] = true;
                Paint(
                    labels[slot],
                    displayName,
                    row.ResourceId.ToString(),
                    ResourceLedger.Get(stock, row.ResourceId));
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

        static void Paint(TMP_Text valueLabel, string displayName, string resourceId, float amount)
        {
            if (valueLabel == null)
                return;
            SetChipVisible(valueLabel, true);
            valueLabel.text = Mathf.FloorToInt(amount).ToString();
            var title = FindTitle(valueLabel);
            if (title != null)
                title.text = string.IsNullOrEmpty(displayName) ? resourceId : displayName;
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
