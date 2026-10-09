using System;
using UnityEngine;

namespace EndlessDescent.Core
{
    // The handful of things an options screen is for. Kept in PlayerPrefs rather than in a save,
    // because they belong to whoever is at the keyboard and not to the character
    public static class Settings
    {
        public const float MinSensitivity = 0.02f;
        public const float MaxSensitivity = 0.6f;

        public static event Action Changed;

        static bool loaded;

        static float sensitivity = 0.12f;
        static bool invertY;
        static float fieldOfView = 70f;
        static bool questMarkers = true;
        static float volume = 0.8f;

        public static float Sensitivity { get => Value(ref sensitivity); set => Set(ref sensitivity, Mathf.Clamp(value, MinSensitivity, MaxSensitivity), "look.sensitivity"); }
        public static bool InvertY { get => Value(ref invertY); set => Set(ref invertY, value, "look.invert"); }
        public static float FieldOfView { get => Value(ref fieldOfView); set => Set(ref fieldOfView, Mathf.Clamp(value, 55f, 110f), "view.fov"); }
        public static bool QuestMarkers { get => Value(ref questMarkers); set => Set(ref questMarkers, value, "quest.markers"); }
        public static float Volume { get => Value(ref volume); set => Set(ref volume, Mathf.Clamp01(value), "audio.volume"); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }

        static T Value<T>(ref T field)
        {
            Load();
            return field;
        }

        static void Set<T>(ref T field, T value, string key)
        {
            Load();
            field = value;
            Write(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static void Write<T>(string key, T value)
        {
            switch (value)
            {
                case float number: PlayerPrefs.SetFloat(key, number); break;
                case bool flag: PlayerPrefs.SetInt(key, flag ? 1 : 0); break;
            }
        }

        static void Load()
        {
            if (loaded)
                return;

            // Set before reading, so a first run keeps the defaults above rather than zeroing them
            loaded = true;

            sensitivity = PlayerPrefs.GetFloat("look.sensitivity", sensitivity);
            invertY = PlayerPrefs.GetInt("look.invert", 0) == 1;
            fieldOfView = PlayerPrefs.GetFloat("view.fov", fieldOfView);
            questMarkers = PlayerPrefs.GetInt("quest.markers", 1) == 1;
            volume = PlayerPrefs.GetFloat("audio.volume", volume);
        }
    }
}
