using System.IO;
using TheyWillDescend.Infrastructure.Logging;
using UnityEngine;

namespace TheyWillDescend.Infrastructure.Save
{
    /// <summary>
    /// One-slot JSON store. Temporary: not DOTS SerializeUtility.
    /// </summary>
    public static class RunSnapshotStore
    {
        public const int Version = 3;

        public static string SlotPath =>
            Path.Combine(Application.persistentDataPath, "run_slot0.json");

        public static void Write(RunSnapshot snapshot)
        {
            snapshot.version = Version;
            File.WriteAllText(SlotPath, JsonUtility.ToJson(snapshot, prettyPrint: true));
            GameLog.Info($"Saved slot → {SlotPath}");
        }

        public static bool TryRead(out RunSnapshot snapshot)
        {
            snapshot = null;
            if (!File.Exists(SlotPath))
            {
                GameLog.Warning($"No slot at {SlotPath}");
                return false;
            }

            var json = File.ReadAllText(SlotPath);
            snapshot = JsonUtility.FromJson<RunSnapshot>(json);
            if (snapshot == null)
            {
                GameLog.Error("Slot JSON failed to parse.");
                return false;
            }

            GameLog.Info($"Loaded slot ← {SlotPath}");
            return true;
        }
    }
}
