using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// Process-owned curtain (child of AppScope). Survives both the scene we leave and the one we
    /// enter. Only <see cref="Shell.TransitionService"/> shows or hides it.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LoadingScreen : MonoBehaviour
    {
        [SerializeField] TMP_Text status;
        [SerializeField] Image progressFill;
        [SerializeField, Min(0f)] float fadeSeconds = 0.25f;
        [SerializeField] string defaultStatus = "Loading...";

        CanvasGroup _group;

        public bool IsVisible { get; private set; }

        /// <summary>The object stays inactive in the prefab, so Awake may not have run yet.</summary>
        CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        public async UniTask Show(CancellationToken cancellationToken)
        {
            Open();
            await Fade(1f, cancellationToken);
        }

        /// <summary>First frame of a scene opened directly: no fade-in, the world must not flash.</summary>
        public void ShowImmediate()
        {
            Open();
            Group.alpha = 1f;
        }

        public async UniTask Hide(CancellationToken cancellationToken)
        {
            if (!IsVisible)
                return;
            await Fade(0f, cancellationToken);
            IsVisible = false;
            gameObject.SetActive(false);
        }

        public void SetProgress(float progress, string label)
        {
            if (progressFill != null)
                progressFill.fillAmount = Mathf.Clamp01(progress);
            if (status != null)
                status.text = string.IsNullOrEmpty(label) ? defaultStatus : label;
        }

        void Open()
        {
            IsVisible = true;
            gameObject.SetActive(true);
            SetProgress(0f, null);
        }

        async UniTask Fade(float target, CancellationToken cancellationToken)
        {
            var group = Group;
            var start = group.alpha;
            if (fadeSeconds <= 0f || Mathf.Approximately(start, target))
            {
                group.alpha = target;
                return;
            }

            for (var t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(start, target, t / fadeSeconds);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            group.alpha = target;
        }
    }
}
