using System.Text;
using EndlessDescent.Combat;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Quests;
using EndlessDescent.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace EndlessDescent.UI
{
    // What a play session is actually doing, on screen, in development builds. Every harness this
    // project has measures the engine free half, this is the first thing that measures a frame
    // F3 shows it
    [DisallowMultipleComponent]
    public class DebugOverlay : MonoBehaviour
    {
        const int FrameSamples = 120;

        readonly float[] frames = new float[FrameSamples];
        readonly StringBuilder text = new StringBuilder();

        PlaceLoader place;
        WorldTerrainStreamer world;
        QuestJournal journal;
        AiActivation ai;
        Journey journey;
        WorldWater water;
        EndlessDescent.Dungeons.DungeonVisibility culling;
        Transform player;

        int nextFrame;
        bool shown;
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Debug.isDebugBuild)
                return;

            Attach();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            if (FindFirstObjectByType<DebugOverlay>() != null)
                return;

            new GameObject("Debug Overlay").AddComponent<DebugOverlay>();
        }

        void Update()
        {
            frames[nextFrame++ % FrameSamples] = Time.unscaledDeltaTime;

            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
                shown = !shown;
        }

        void Find()
        {
            if (place == null) place = FindFirstObjectByType<PlaceLoader>();
            if (world == null) world = FindFirstObjectByType<WorldTerrainStreamer>();
            if (journal == null) journal = FindFirstObjectByType<QuestJournal>();
            if (ai == null) ai = FindFirstObjectByType<AiActivation>();
            if (journey == null) journey = FindFirstObjectByType<Journey>();
            if (water == null) water = FindFirstObjectByType<WorldWater>();
            if (culling == null) culling = FindFirstObjectByType<EndlessDescent.Dungeons.DungeonVisibility>();
            if (player == null && place != null) player = place.transform;
        }

        void OnGUI()
        {
            if (!shown)
                return;

            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = false };

            Find();
            Compose();

            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(8f, 8f, 460f, 310f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(16f, 12f, 450f, 304f), text.ToString(), style);
        }

        void Compose()
        {
            float worst = 0f, total = 0f;

            foreach (float frame in frames)
            {
                worst = Mathf.Max(worst, frame);
                total += frame;
            }

            text.Clear();
            text.AppendLine($"frame  {total / FrameSamples * 1000f:0.0} ms avg   {worst * 1000f:0.0} ms worst of {FrameSamples}");
            text.AppendLine($"space  {CurrentSpace.Kind}{(Transition.Busy ? "   (transition)" : string.Empty)}");

            // Streaming throughput against what travel is asking of it, the two numbers that decide
            // whether auto run stutters
            if (world != null)
            {
                text.AppendLine($"ground {world.Resident} near + {world.FarResident} far   " +
                                $"surface {(world.Ready ? "ready" : "building")}");
                text.AppendLine($"build  {world.TilesPerSecond:0} tiles/s   " +
                                $"{world.Building} on workers   {world.Backlog} queued");
            }

            if (water != null)
                text.AppendLine($"water  {water.Tiles} tiles   {water.Lakes} lakes"
                                + (water.Starved > 0 ? $"   {water.Starved} STARVED (will flood)" : string.Empty));

            if (journey != null && journey.Travelling)
                text.AppendLine($"travel {journey.Pace:0} m/s of {journey.Ceiling:0} asked" +
                                $"{(journey.Waiting ? "   WAITING for ground" : string.Empty)}");

            if (place != null)
            {
                Vector2 km = place.HereKm;
                text.AppendLine($"at     {km.x:0.0}, {km.y:0.0} km   origin offset {WorldOrigin.Offset.x:0}, {WorldOrigin.Offset.z:0} m");
                text.AppendLine($"place  town {Name(place.Region, place.BuiltSettlement)}   " +
                                $"dungeon {Name(place.Region, place.PreparedDungeon)}{(place.DungeonReady ? " (ready)" : string.Empty)}");
            }

            // Which rooms the culling believes the player is in. When a corridor goes dark, this is
            // the line that says whether it guessed the wrong room
            if (culling != null && CurrentSpace.Kind == SpaceKind.Dungeon)
                text.AppendLine($"cull   in room {string.Join(", ", culling.Rooms)}   drawing {culling.VisibleRooms} rooms");

            text.AppendLine($"alive  {EnemyBrain.Alive.Count} enemies   {(ai != null ? ai.Thinking : 0)} thinking");

            if (journal != null)
            {
                text.AppendLine($"quests {journal.Active.Count} active");

                foreach (Quest quest in journal.Active)
                {
                    QuestStage stage = quest.CurrentStage;
                    string step = stage != null && stage.Objectives.Count > 0
                        ? $"{stage.Objectives[0].Progress}/{stage.Objectives[0].Data.Count} {stage.Objectives[0].Description}"
                        : "no objective";

                    text.AppendLine($"  {quest.Title} - {step}");
                }
            }
        }

        static string Name(RegionMap region, int index) =>
            region != null && region.Has(index) ? region.Locations[index].DisplayName : "none";
    }
}
