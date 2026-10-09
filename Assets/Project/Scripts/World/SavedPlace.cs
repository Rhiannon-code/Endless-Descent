using System;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EndlessDescent.World
{
    [Serializable]
    public struct SavedPlaceState
    {
        public bool Known;
        public double X;
        public float Y;
        public double Z;
        public float Yaw;
    }

    // Where the player last stood outdoors, which is what a save comes back to. A save made in a
    // dungeon or a building therefore comes back at its door: nothing inside either is saved, both
    // are rebuilt from their seed. Kept in doubles, a place can be 279 km from the origin
    [DisallowMultipleComponent]
    public class SavedPlace : MonoBehaviour, ISaveable
    {
        [SerializeField] string saveKey = "place.player";

        PlaceLoader place;
        SavedPlaceState last;
        SavedPlaceState? pending;

        public string SaveKey => saveKey;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Attach();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            PlaceLoader loader = FindFirstObjectByType<PlaceLoader>();

            if (loader != null && loader.GetComponent<SavedPlace>() == null)
                loader.gameObject.AddComponent<SavedPlace>();
        }

        void Awake() => place = GetComponent<PlaceLoader>();

        void LateUpdate()
        {
            Transform player = place != null ? place.Player : null;

            if (player == null || Transition.Busy || CurrentSpace.Kind != SpaceKind.Overworld)
                return;

            Vector3 at = player.position;

            last = new SavedPlaceState
            {
                Known = true,
                X = (double)WorldOrigin.Offset.x + at.x,
                Y = at.y,
                Z = (double)WorldOrigin.Offset.z + at.z,
                Yaw = player.eulerAngles.y
            };
        }

        public string CaptureJson() => JsonUtility.ToJson(last);

        // In the world the move happens now. From the front end the prewarm is already in the dark and
        // takes it as its arrival instead (TakePending)
        public void RestoreJson(string json)
        {
            SavedPlaceState state = JsonUtility.FromJson<SavedPlaceState>(json);

            if (!state.Known)
                return;

            last = state;

            if (Transition.Busy)
                pending = state;
            else
                place?.ReturnTo(Spot(state), state.Yaw);
        }

        public bool TakePending(out Vector3 worldMetres, out float yaw)
        {
            worldMetres = pending.HasValue ? Spot(pending.Value) : default;
            yaw = pending.HasValue ? pending.Value.Yaw : 0f;

            bool had = pending.HasValue;
            pending = null;
            return had;
        }

        static Vector3 Spot(SavedPlaceState state) => new Vector3((float)state.X, state.Y, (float)state.Z);
    }
}
