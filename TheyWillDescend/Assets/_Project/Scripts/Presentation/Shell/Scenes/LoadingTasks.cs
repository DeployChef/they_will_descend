using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TheyWillDescend.Presentation.Infrastructure;

namespace TheyWillDescend.Presentation.Shell
{
    /// <summary>
    /// A scene says "I need time" here when it activates. <see cref="TransitionService"/>
    /// keeps the loading screen up until every task ends. A task fails by throwing.
    /// </summary>
    public sealed class LoadingTasks
    {
        sealed class Entry
        {
            public float Weight;
            public string Label;
            public bool Done;
            public Exception Error;
        }

        readonly List<Entry> _entries = new();

        /// <summary>Raised when the list goes from empty to busy.</summary>
        public event Action WorkAdded;

        public bool HasWork => _entries.Count > 0;

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
        /// Returns false if any failed. The list is empty afterwards.
        /// </summary>
        public async UniTask<bool> WaitAll(Action<float, string> progress, CancellationToken cancellationToken)
        {
            while (!AllDone())
            {
                progress?.Invoke(Progress(), CurrentLabel());
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            progress?.Invoke(1f, null);

            var ok = true;
            for (var i = 0; i < _entries.Count; i++)
            {
                var error = _entries[i].Error;
                if (error == null)
                    continue;
                ok = false;
                GameLog.Error($"Loading task '{_entries[i].Label}' failed: {error.Message}");
            }

            _entries.Clear();
            return ok;
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
