using TheyWillDescend.Simulation.Io;
using TMPro;
using UnityEngine;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Pulls ResourceStock into scene-authored labels. Does not build UI.
    /// </summary>
    public sealed class ResourceWidget : MonoBehaviour
    {
        [SerializeField] TMP_Text resource1Label;
        [SerializeField] TMP_Text resource2Label;
        [SerializeField] TMP_Text resource3Label;
        [SerializeField] TMP_Text resource4Label;

        void Update()
        {
            if (!SimIo.TryGetStock(out var stock))
            {
                Paint(resource1Label, 0);
                Paint(resource2Label, 0);
                Paint(resource3Label, 0);
                Paint(resource4Label, 0);
                return;
            }

            Paint(resource1Label, Mathf.FloorToInt(stock.Resource1));
            Paint(resource2Label, Mathf.FloorToInt(stock.Resource2));
            Paint(resource3Label, Mathf.FloorToInt(stock.Resource3));
            Paint(resource4Label, Mathf.FloorToInt(stock.Resource4));
        }

        static void Paint(TMP_Text label, int value)
        {
            if (label != null)
                label.text = value.ToString();
        }
    }
}
