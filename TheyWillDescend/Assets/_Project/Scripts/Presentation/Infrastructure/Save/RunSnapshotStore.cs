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
        public static string SlotPath =>
            Path.Combine(Application.persistentDataPath, "run_slot0.json");

        public static void Write(RunSnapshot snapshot)
        {
            snapshot.version = RunSnapshot.CurrentVersion;
            File.WriteAllText(SlotPath, JsonUtility.ToJson(snapshot, prettyPrint: true));
            GameLog.Info($"Saved slot → {SlotPath}");
        }

        public static void DeleteSlot()
        {
            if (!File.Exists(SlotPath))
                return;
            File.Delete(SlotPath);
            GameLog.Info($"Deleted slot → {SlotPath}");
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
                DeleteSlot();
                return false;
            }

            if (snapshot.version != RunSnapshot.CurrentVersion)
            {
                GameLog.Warning(
                    $"Slot v{snapshot.version} != current v{RunSnapshot.CurrentVersion}; deleting {SlotPath}.");
                DeleteSlot();
                snapshot = null;
                return false;
            }

            GameLog.Info($"Loaded slot ← {SlotPath}");
            return true;
        }
    }
}
