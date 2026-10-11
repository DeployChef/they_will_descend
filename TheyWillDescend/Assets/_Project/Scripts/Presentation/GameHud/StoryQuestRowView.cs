using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>Quest row prefab. The widget instantiates one per live quest.</summary>
    public sealed class StoryQuestRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text description;
        [SerializeField] TMP_Text progress;
        [SerializeField] Slider deadline;

        public void Show(string titleText, string descriptionText, string progressText, bool timed, float fill)
        {
            if (title != null)
                title.text = titleText;
            if (description != null)
                description.text = descriptionText;
            if (progress != null)
                progress.text = progressText;
            if (deadline == null)
                return;
            deadline.gameObject.SetActive(timed);
            if (timed)
                deadline.value = fill;
        }
    }
}
