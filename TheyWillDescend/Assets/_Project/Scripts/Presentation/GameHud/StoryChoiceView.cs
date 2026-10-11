using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>Choice button prefab. The panel instantiates it into a row built on the prefab.</summary>
    public sealed class StoryChoiceView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text label;

        public Button Button => button;

        public void SetLabel(string text)
        {
            if (label != null)
                label.text = text;
        }
    }
}
