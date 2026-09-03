using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// One tech slot authored on the research HUD prefab. The widget only fills it.
    /// </summary>
    public sealed class ResearchNodeView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image background;
        [SerializeField] Image fill;
        [SerializeField] TMP_Text label;

        public Button Button => button;
        public Image Background => background;
        public Image Fill => fill;
        public TMP_Text Label => label;
        public string TechId { get; set; }

        public void Bind(Button button, Image background, Image fill, TMP_Text label)
        {
            this.button = button;
            this.background = background;
            this.fill = fill;
            this.label = label;
        }
    }
}
