using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EndlessDescent.Core
{
    [Serializable]
    public class SaveEntry
    {
        public string Key;
        public string Json;
    }

    [Serializable]
    public class GameSave
    {
        public int Version = 1;
        public string SavedAtIso;
        public double WorldMinutes;
        public List<SaveEntry> Entries = new List<SaveEntry>();
    }

    public static class SaveSystem
    {
        public const int CurrentVersion = 1;

        public static string DefaultDirectory => Path.Combine(Application.persistentDataPath, "saves");

        public static string PathFor(string slot) => Path.Combine(DefaultDirectory, slot + ".json");

        public static bool Save(string slot, IEnumerable<ISaveable> participants, double worldMinutes)
        {
            GameSave save = new GameSave
            {
                Version = CurrentVersion,
                SavedAtIso = DateTime.UtcNow.ToString("o"),
                WorldMinutes = worldMinutes
            };

            foreach (ISaveable participant in participants)
            {
                if (participant == null)
                    continue;

                save.Entries.Add(new SaveEntry { Key = participant.SaveKey, Json = participant.CaptureJson() });
            }

            // Written beside the slot and swapped in whole, so a crash halfway through leaves the old
            // save as it was rather than a half written file in its place
            try
            {
                Directory.CreateDirectory(DefaultDirectory);

                string path = PathFor(slot);
                string partial = path + ".partial";

                File.WriteAllText(partial, JsonUtility.ToJson(save, true));

                if (File.Exists(path))
                    File.Replace(partial, path, null);
                else
                    File.Move(partial, path);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Save to slot '{slot}' failed: {e.Message}");
                return false;
            }
        }

        public static GameSave Load(string slot)
        {
            string path = PathFor(slot);
            if (!File.Exists(path))
                return null;

            try
            {
                GameSave save = JsonUtility.FromJson<GameSave>(File.ReadAllText(path));

                // Fields a newer game added would be silently dropped by this one, and saving again
                // would lose them for good
                if (save != null && save.Version > CurrentVersion)
                {
                    Debug.LogError($"Slot '{slot}' was saved by a newer version ({save.Version}) than this one ({CurrentVersion}).");
                    return null;
                }

                return save;
            }
            catch (Exception e)
            {
                Debug.LogError($"Load of slot '{slot}' failed: {e.Message}");
                return null;
            }
        }

        public static void Apply(GameSave save, IEnumerable<ISaveable> participants)
        {
            if (save == null)
                return;

            Dictionary<string, string> byKey = new Dictionary<string, string>();
            foreach (SaveEntry entry in save.Entries)
                byKey[entry.Key] = entry.Json;

            foreach (ISaveable participant in participants)
            {
                if (participant != null && byKey.TryGetValue(participant.SaveKey, out string json))
                    participant.RestoreJson(json);
            }
        }

        public static bool Delete(string slot)
        {
            string path = PathFor(slot);
            if (!File.Exists(path))
                return false;

            File.Delete(path);
            return true;
        }

        public static List<string> ListSlots()
        {
            List<string> slots = new List<string>();
            if (!Directory.Exists(DefaultDirectory))
                return slots;

            // Newest first, the one most likely wanted is at the top
            string[] files = Directory.GetFiles(DefaultDirectory, "*.json");
            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));

            foreach (string file in files)
                slots.Add(Path.GetFileNameWithoutExtension(file));

            return slots;
        }
    }
}
