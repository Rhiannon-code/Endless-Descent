using System.Collections.Generic;
using EndlessDescent.Combat;
using EndlessDescent.Data;
using EndlessDescent.Dungeons;
using EndlessDescent.Items;
using EndlessDescent.Quests;
using EndlessDescent.Simulation;
using EndlessDescent.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace EndlessDescent.UI
{
    // F4 in development builds: a marker over wherever each active quest's current step is. For testing
    // only, the game means the player to work it out from the journal and from what they were told
    [DisallowMultipleComponent]
    public class DebugWaypoint : MonoBehaviour
    {
        const float RefreshSeconds = 0.5f;

        struct Waypoint
        {
            public string Label;
            public Transform Follow;
            public Vector2 Km;
            public float Height;
            public bool Placed;
        }

        readonly List<Waypoint> waypoints = new List<Waypoint>();

        PlaceLoader place;
        QuestJournal journal;
        NpcDirectory npcs;
        DungeonGenerator dungeons;
        bool shown;
        float nextRefresh;
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
            if (FindFirstObjectByType<DebugWaypoint>() == null)
                new GameObject("Debug Waypoint").AddComponent<DebugWaypoint>();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame)
            {
                shown = !shown;
                nextRefresh = 0f;
            }

            if (!shown || Time.unscaledTime < nextRefresh)
                return;

            nextRefresh = Time.unscaledTime + RefreshSeconds;
            Refresh();
        }

        void Refresh()
        {
            if (place == null) place = FindFirstObjectByType<PlaceLoader>();
            if (journal == null) journal = FindFirstObjectByType<QuestJournal>();
            if (npcs == null) npcs = FindFirstObjectByType<NpcDirectory>();

            waypoints.Clear();

            if (journal == null)
                return;

            foreach (Quest quest in journal.Active)
            {
                QuestObjectiveData objective = QuestDirections.CurrentObjective(quest);

                if (objective == null)
                    continue;

                string label = $"{quest.Title}: {objective.Description}";
                Transform near = Nearest(objective);

                if (near != null)
                {
                    waypoints.Add(new Waypoint { Label = label, Follow = near, Placed = true });
                    continue;
                }

                if (place == null || place.Region == null || place.World == null ||
                    !QuestDirections.TryFind(quest, place.Region, place.HereKm, out QuestDirections.Heading heading))
                {
                    waypoints.Add(new Waypoint { Label = $"{label}   (no known place)" });
                    continue;
                }

                waypoints.Add(new Waypoint
                {
                    Label = $"{label}   ({heading.Place})",
                    Km = heading.Km,
                    Height = place.World.Surface.Height(heading.Km),
                    Placed = true
                });
            }

            // Whoever has work to offer, so the conversation that starts a quest can be found as well
            if (npcs == null)
                return;

            foreach (NpcActor actor in npcs.Actors)
            {
                if (actor == null || !actor.isActiveAndEnabled || !actor.TryGetComponent(out NpcDialogue talk))
                    continue;

                string offered = talk.OfferedTitle();

                if (offered != null)
                    waypoints.Add(new Waypoint { Label = $"Work: {offered}   ({actor.DisplayName})", Follow = actor.transform, Placed = true });
            }
        }

        // The thing itself when it is loaded close by, the person, the creature or the item
        Transform Nearest(QuestObjectiveData objective)
        {
            if (objective.Kind == QuestObjectiveKind.ClearDungeon)
                return BossRoom(objective);

            Transform player = place != null ? place.Player : null;

            if (objective.Npc != null)
            {
                NpcActor actor = npcs != null ? npcs.Find(objective.Npc) : null;
                return actor != null && actor.IsAlive && actor.isActiveAndEnabled ? actor.transform : null;
            }

            if (player == null)
                return null;

            Transform best = null;
            float closest = float.MaxValue;

            void Consider(Transform candidate)
            {
                float distance = (candidate.position - player.position).sqrMagnitude;

                if (distance >= closest)
                    return;

                closest = distance;
                best = candidate;
            }

            if (objective.Kind == QuestObjectiveKind.KillEnemy && objective.Enemy != null)
            {
                foreach (EnemyBrain brain in EnemyBrain.Alive)
                {
                    if (brain == null || brain.Definition != objective.Enemy)
                        continue;

                    if (brain.TryGetComponent(out Health health) && health.IsAlive)
                        Consider(brain.transform);
                }
            }

            if (objective.Kind == QuestObjectiveKind.CollectItem && objective.Item != null)
            {
                foreach (ItemPickup pickup in FindObjectsByType<ItemPickup>(FindObjectsSortMode.None))
                    if (pickup.Item == objective.Item) Consider(pickup.transform);
            }

            return best;
        }

        // Underground the place on the surface is no help. A clearing is done at the boss, in the last room
        // of the main route, which exists from the moment the dungeon is built, before its floor fills
        Transform BossRoom(QuestObjectiveData objective)
        {
            if (place == null || !place.Inside || place.Region == null)
                return null;

            if (dungeons == null)
                dungeons = FindFirstObjectByType<DungeonGenerator>();

            DungeonGraph graph = dungeons != null ? dungeons.Graph : null;

            if (graph == null)
                return null;

            string here = place.Region.Has(place.PreparedDungeon)
                ? place.Region.Locations[place.PreparedDungeon].DisplayName
                : null;

            if (objective.Location != null && objective.Location.DisplayName != here)
                return null;

            return dungeons.Current.Rooms.TryGetValue(graph.BossId, out GameObject room) ? room.transform : null;
        }

        void OnGUI()
        {
            if (!shown)
                return;

            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false };

            Camera view = Camera.main;

            if (waypoints.Count == 0)
            {
                Box(new Vector2(Screen.width * 0.5f, 40f), "F4 waypoint: no active quest step");
                return;
            }

            float listed = 40f;

            foreach (Waypoint waypoint in waypoints)
            {
                if (view == null || !waypoint.Placed)
                {
                    Box(new Vector2(Screen.width * 0.5f, listed), waypoint.Label);
                    listed += 24f;
                    continue;
                }

                Vector3 target = waypoint.Follow != null
                    ? waypoint.Follow.position + Vector3.up * 2.2f
                    : WorldOrigin.UnityAt(waypoint.Km, waypoint.Height + 2f);

                float metres = Vector3.Distance(view.transform.position, target);
                string distance = metres >= 1000f ? $"{metres / 1000f:0.0} km" : $"{metres:0} m";

                Box(OnScreen(view, target), $"<> {waypoint.Label}   {distance}");
            }
        }

        // Behind the camera it drops to the bottom edge, on the side you would turn towards
        static Vector2 OnScreen(Camera view, Vector3 target)
        {
            Vector3 point = view.WorldToScreenPoint(target);

            if (point.z < 0f)
                point = new Vector3(Screen.width - point.x, 0f, 0f);

            const float margin = 60f;
            return new Vector2(
                Mathf.Clamp(point.x, margin * 3f, Screen.width - margin * 3f),
                Mathf.Clamp(Screen.height - point.y, margin, Screen.height - margin));
        }

        void Box(Vector2 centre, string text)
        {
            Vector2 size = style.CalcSize(new GUIContent(text)) + new Vector2(12f, 6f);
            Rect rect = new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, size.x, size.y);

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.45f, 0.85f, 1f);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 3f, size.x, size.y), text, style);
            GUI.color = Color.white;
        }
    }
}
