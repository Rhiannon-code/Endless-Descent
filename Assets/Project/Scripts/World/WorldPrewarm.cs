using System.Collections;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Player;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.World
{
    // The opening of a game, once there is a front end in front of it. Everything that used to
    // happen in the first few seconds of play, waking the surface, warming shaders, putting up the
    // town you start in, happens here instead, while the menu is still on screen and there is a
    // caption saying what is being waited for
    [DisallowMultipleComponent]
    public class WorldPrewarm : MonoBehaviour
    {
        [SerializeField] PlaceLoader place;
        [SerializeField] ShaderWarmup shaders;
        [SerializeField] SaveCoordinator saves;
        [SerializeField] CharacterSetup character;

        public float Progress { get; private set; }
        public string Caption { get; private set; } = "Waiting";
        public bool Finished { get; private set; }

        // Awake, not Start, PlaceLoader begins its own opening entry in Start, and taking it over has
        // to happen before that rather than race it
        void Awake()
        {
            if (GameStart.Requested)
                place?.DeferOpeningEntry();
        }

        void Start()
        {
            if (!GameStart.Requested)
            {
                Finished = true;
                return;
            }

            Transition.StartInDark("Preparing the world", Run(), () => Finished = true);
        }

        IEnumerator Run()
        {
            // Only does anything when a variant collection has been recorded and assigned; with
            // none it falls straight through (ShaderWarmup explains why there is no fallback)
            Report(0.05f, "Warming shaders");

            if (shaders != null)
                yield return shaders.Run();

            Report(0.25f, "Waking the world");

            WorldTerrainStreamer world = place != null ? place.World : null;

            while (world != null && !world.Ready)
                yield return Steps.WaitFrame;

            Report(0.4f, "Making your character");
            ApplyStart();

            Report(0.55f, "Building the ground you start on");

            // A loaded game comes back where it was saved, not at the start
            SavedPlace saved = place != null ? place.GetComponent<SavedPlace>() : null;

            if (saved != null && saved.TakePending(out Vector3 spot, out float yaw))
                yield return place.Returning(spot, yaw);
            else if (place != null)
                yield return place.OpeningArrival();

            Report(1f, "Ready");
            GameStart.Clear();
        }

        // A loaded game restores over the character the creator would have made, so the order is the
        // same either way and nothing has to know which happened
        void ApplyStart()
        {
            if (GameStart.Kind == GameStartKind.NewCharacter)
            {
                character?.Apply(GameStart.Blueprint);
                return;
            }

            if (GameStart.Kind == GameStartKind.ContinueSave && saves != null && !saves.Load(GameStart.Slot))
                Debug.LogWarning($"No save in slot '{GameStart.Slot}'; starting fresh instead.", this);
        }

        void Report(float progress, string caption)
        {
            Progress = Mathf.Clamp01(progress);
            Caption = caption;
            Transition.Report(caption);
        }
    }
}
