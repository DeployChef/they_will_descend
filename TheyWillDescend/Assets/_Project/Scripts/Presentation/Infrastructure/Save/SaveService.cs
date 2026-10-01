using System.IO;
using TheyWillDescend.App;
using TheyWillDescend.Infrastructure.Logging;
using UnityEngine;

namespace TheyWillDescend.Infrastructure.Save
{
    /// <summary>
    /// One-slot JSON store on the root scope. Temporary file format, not DOTS SerializeUtility.
    /// Capture and apply of the ECS write model stay in <see cref="RunSessionSnapshot"/>.
    /// </summary>
    public sealed class SaveService
    {
        public string SlotPath =>
            Path.Combine(Application.persistentDataPath, "run_slot0.json");

        public bool HasSlot => File.Exists(SlotPath);

        public void SaveCurrent()
        {
            Write(RunSessionSnapshot.Capture());
        }

        public bool TryRead(out RunSnapshot snapshot)
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
                Delete();
                return false;
            }

            if (snapshot.version != RunSnapshot.CurrentVersion)
            {
                GameLog.Warning(
                    $"Slot v{snapshot.version} != current v{RunSnapshot.CurrentVersion}; deleting {SlotPath}.");
                Delete();
                snapshot = null;
                return false;
            }

            GameLog.Info($"Loaded slot ← {SlotPath}");
            return true;
        }

        void Write(RunSnapshot snapshot)
        {
            snapshot.version = RunSnapshot.CurrentVersion;
            File.WriteAllText(SlotPath, JsonUtility.ToJson(snapshot, prettyPrint: true));
            GameLog.Info($"Saved slot → {SlotPath}");
        }

        void Delete()
        {
            if (!File.Exists(SlotPath))
                return;
            File.Delete(SlotPath);
            GameLog.Info($"Deleted slot → {SlotPath}");
        }
    }
}
