using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Presentation.Infrastructure;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// Process-owned curtain (child of AppScope). Survives both the scene we leave and the one we enter.
    /// A scene adds work with <see cref="Add"/>; <see cref="Wait"/> keeps the curtain up and draws progress
    /// until every task ends. A failed task throws. Only <see cref="Shell.ShellService"/> shows or hides it.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LoadingScreen : MonoBehaviour
    {
        sealed class Entry
        {
            public float Weight;
            public string Label;
            public bool Done;
            public Exception Error;
        }

        [SerializeField] TMP_Text status;
        [SerializeField] Image progressFill;
        [SerializeField, Min(0f)] float fadeSeconds = 0.25f;
        [SerializeField] string defaultStatus = "Loading...";

        readonly List<Entry> _entries = new();
        CanvasGroup _group;

        /// <summary>Raised when the list goes from empty to busy.</summary>
        public event Action WorkAdded;

        public bool IsVisible { get; private set; }

        /// <summary>The object stays inactive in the prefab, so Awake may not have run yet.</summary>
        CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        public void Add(UniTask task, float weight = 1f, string label = null)
        {
            var entry = new Entry { Weight = weight > 0f ? weight : 1f, Label = label };
            _entries.Add(entry);
            Track(task, entry).Forget();
            if (_entries.Count == 1)
                WorkAdded?.Invoke();
        }

        /// <summary>
        /// Waits until every task, including ones added while waiting, has ended.
        /// Throws if any failed. The list is empty afterwards.
        /// </summary>
        public async UniTask Wait(CancellationToken cancellationToken)
        {
            while (!AllDone())
            {
                SetProgress(Progress(), CurrentLabel());
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            SetProgress(1f, null);

            Exception failure = null;
            for (var i = 0; i < _entries.Count; i++)
            {
                var error = _entries[i].Error;
                if (error == null)
                    continue;
                GameLog.Error($"Loading task '{_entries[i].Label}' failed: {error.Message}");
                failure ??= error;
            }

            _entries.Clear();
            if (failure != null)
                throw new InvalidOperationException("A loading task failed.", failure);
        }

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

        void SetProgress(float progress, string label)
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

        static async UniTaskVoid Track(UniTask task, Entry entry)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                entry.Error = e;
            }
            finally
            {
                entry.Done = true;
            }
        }

        bool AllDone()
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (!_entries[i].Done)
                    return false;
            }

            return true;
        }

        float Progress()
        {
            float total = 0f, done = 0f;
            for (var i = 0; i < _entries.Count; i++)
            {
                total += _entries[i].Weight;
                if (_entries[i].Done)
                    done += _entries[i].Weight;
            }

            return total > 0f ? done / total : 1f;
        }

        string CurrentLabel()
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (!_entries[i].Done)
                    return _entries[i].Label;
            }

            return null;
        }
    }
}
