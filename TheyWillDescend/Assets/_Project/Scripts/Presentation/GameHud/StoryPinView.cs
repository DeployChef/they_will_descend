using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>Pin prefab. The board only fills fields that already sit on it.</summary>
    public sealed class StoryPinView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] GameObject exclamation;
        [SerializeField] GameObject question;
        [SerializeField] TMP_Text timer;

        public Button Button => button;

        public void Show(bool questionIcon, bool showTimer, string timerText)
        {
            if (exclamation != null)
                exclamation.SetActive(!questionIcon);
            if (question != null)
                question.SetActive(questionIcon);
            if (timer == null)
                return;
            timer.gameObject.SetActive(showTimer);
            if (showTimer)
                timer.text = timerText;
        }
    }
}
