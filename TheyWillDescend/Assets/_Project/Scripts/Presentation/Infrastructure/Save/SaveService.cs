using System.IO;
using TheyWillDescend.Presentation.SimBridge;
using UnityEngine;

namespace TheyWillDescend.Presentation.Infrastructure
{
    /// <summary>
    /// JSON snapshots on the root scope. A save id is the file stem: <c>run_{id}.json</c>.
    /// <see cref="Slot0"/> is the one file that already exists. Capture and apply of the ECS
    /// write model stay in <see cref="RunSessionSnapshot"/>. Temporary format, not DOTS SerializeUtility.
    /// </summary>
    public sealed class SaveService
    {
        public const string Slot0 = "slot0";

        public bool Exists(string saveId) => File.Exists(PathFor(saveId));

        public void SaveCurrent() => Write(Slot0, RunSessionSnapshot.Capture());

        public bool TryRead(string saveId, out RunSnapshot snapshot)
        {
            snapshot = null;
            var path = PathFor(saveId);
            if (path == null)
            {
                GameLog.Error($"Save id '{saveId}' is not a file name.");
                return false;
            }

            if (!File.Exists(path))
            {
                GameLog.Warning($"No save at {path}");
                return false;
            }

            var json = File.ReadAllText(path);
            snapshot = JsonUtility.FromJson<RunSnapshot>(json);
            if (snapshot == null)
            {
                GameLog.Error($"Save JSON failed to parse: {path}");
                Delete(path);
                return false;
            }

            if (snapshot.version != RunSnapshot.CurrentVersion)
            {
                GameLog.Warning(
                    $"Save v{snapshot.version} != current v{RunSnapshot.CurrentVersion}; deleting {path}.");
                Delete(path);
                snapshot = null;
                return false;
            }

            GameLog.Info($"Loaded save ← {path}");
            return true;
        }

        /// <summary><c>run_{id}.json</c> under persistent data. Null when <paramref name="saveId"/> is not a file name.</summary>
        public static string PathFor(string saveId)
        {
            if (string.IsNullOrWhiteSpace(saveId))
                return null;
            saveId = saveId.Trim();
            if (saveId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return null;
            return Path.Combine(Application.persistentDataPath, "run_" + saveId + ".json");
        }

        void Write(string saveId, RunSnapshot snapshot)
        {
            var path = PathFor(saveId);
            if (path == null)
            {
                GameLog.Error($"Save id '{saveId}' is not a file name.");
                return;
            }

            snapshot.version = RunSnapshot.CurrentVersion;
            File.WriteAllText(path, JsonUtility.ToJson(snapshot, prettyPrint: true));
            GameLog.Info($"Saved → {path}");
        }

        static void Delete(string path)
        {
            if (!File.Exists(path))
                return;
            File.Delete(path);
            GameLog.Info($"Deleted save → {path}");
        }
    }
}
