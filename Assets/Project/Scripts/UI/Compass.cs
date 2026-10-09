using System.Collections.Generic;
using EndlessDescent.Quests;
using EndlessDescent.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // Which way the camera faces, along the top of the screen. It puts itself on whatever HUD the scene
    // has, so no scene needs rebuilding for it
    [DisallowMultipleComponent]
    public class Compass : MonoBehaviour
    {
        const float Width = 640f;
        const float Height = 30f;
        const float Span = 180f;
        const float Step = 15f;
        const float MarkWidth = 40f;

        static readonly string[] Points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        const int MostMarkers = 4;

        RectTransform[] marks;
        Text readout;
        int shown = -1;

        readonly List<Text> markers = new List<Text>();
        PlaceLoader place;
        QuestJournal journal;
        float nextLook;

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
            foreach (PlayerHud hud in FindObjectsByType<PlayerHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hud.GetComponentInChildren<Compass>(true) != null)
                    continue;

                GameObject compass = new GameObject("Compass", typeof(RectTransform));
                compass.transform.SetParent(hud.transform, false);

                // Behind everything else on the canvas, so the menu opens over it
                compass.transform.SetAsFirstSibling();
                compass.AddComponent<Compass>();
            }
        }

        void Awake()
        {
            RectTransform root = (RectTransform)transform;
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -14f);
            root.sizeDelta = new Vector2(Width, Height + 22f);

            RectTransform strip = MenuWidgets.Panel(root, "Strip", Vector2.zero, new Vector2(Width, Height));
            strip.gameObject.AddComponent<RectMask2D>();
            MenuWidgets.Block(strip, Vector2.zero, new Vector2(Width, Height), new Color(0.05f, 0.05f, 0.07f, 0.55f));

            marks = new RectTransform[Mathf.RoundToInt(360f / Step)];

            for (int i = 0; i < marks.Length; i++)
            {
                float angle = i * Step;
                bool named = angle % 45f == 0f;
                bool cardinal = angle % 90f == 0f;

                RectTransform mark = MenuWidgets.Panel(strip, $"Mark {angle:0}", Vector2.zero, new Vector2(MarkWidth, Height));
                MenuWidgets.Block(mark, new Vector2(MarkWidth * 0.5f - 1f, 0f), new Vector2(2f, named ? 8f : 5f),
                    new Color(1f, 1f, 1f, named ? 0.9f : 0.45f));

                if (named)
                {
                    Text label = MenuWidgets.Label(mark, new Vector2(0f, -8f), MarkWidth, Points[i / 3], cardinal ? 15 : 11);
                    label.alignment = TextAnchor.MiddleCenter;
                    label.color = angle == 0f ? new Color(1f, 0.55f, 0.4f) : Color.white;
                }

                marks[i] = mark;
            }

            MenuWidgets.Block(root, new Vector2(Width * 0.5f - 1f, 0f), new Vector2(2f, Height), new Color(1f, 0.8f, 0.3f));

            readout = MenuWidgets.Label(root, new Vector2(0f, -Height - 2f), Width, string.Empty, 12);
            readout.alignment = TextAnchor.UpperCenter;

            // Where the work is, which is the one thing Daggerfall never told anybody. On the root
            // rather than the strip, the strip is masked to its own thirty pixels
            for (int i = 0; i < MostMarkers; i++)
            {
                Text marker = MenuWidgets.Label(root, Vector2.zero, 180f, string.Empty, 11);
                marker.alignment = TextAnchor.MiddleCenter;
                marker.color = new Color(0.45f, 0.85f, 1f);
                marker.enabled = false;
                markers.Add(marker);
            }
        }

        // Where each marked place is, worked out twice a second. Every frame it was recomputed for
        // every quest and every marker was switched off and on again, which rebuilt the canvas each frame
        readonly List<Vector2> headings = new List<Vector2>();
        int shownMarkers;

        void Look()
        {
            nextLook = Time.time + 0.5f;

            if (place == null) place = FindFirstObjectByType<PlaceLoader>();
            if (journal == null) journal = FindFirstObjectByType<QuestJournal>();

            headings.Clear();

            // Daggerfall Unity's two most-praised options are smaller dungeons and markers you can
            // switch off
            bool marking = place != null && journal != null && place.Region != null && EndlessDescent.Core.Settings.QuestMarkers;

            if (marking)
            {
                Vector2 here = place.HereKm;

                foreach (Quest quest in journal.Active)
                {
                    if (headings.Count >= markers.Count)
                        break;

                    if (!QuestDirections.TryFind(quest, place.Region, here, out QuestDirections.Heading heading))
                        continue;

                    Vector2 away = heading.Km - here;

                    if (away.sqrMagnitude < 0.0001f)
                        continue;

                    markers[headings.Count].text = $"{heading.Place}  {away.magnitude:0} km";
                    headings.Add(heading.Km);
                }
            }

            for (int i = 0; i < markers.Count; i++)
            {
                bool shown = i < headings.Count;
                if (markers[i].enabled != shown) markers[i].enabled = shown;
            }

            shownMarkers = headings.Count;
        }

        void Quests(float facing)
        {
            if (Time.time >= nextLook)
                Look();

            if (shownMarkers == 0 || place == null)
                return;

            Vector2 here = place.HereKm;

            for (int i = 0; i < shownMarkers; i++)
            {
                Vector2 away = headings[i] - here;
                float bearing = Mathf.Repeat(Mathf.Atan2(away.x, away.y) * Mathf.Rad2Deg, 360f);
                float offset = Mathf.DeltaAngle(facing, bearing);

                markers[i].rectTransform.anchoredPosition = new Vector2(
                    Width * 0.5f + offset * Width / Span - 90f, -Height - 14f - (i + 1) * 13f);
            }
        }

        void LateUpdate()
        {
            Camera view = Camera.main;
            if (view == null)
                return;

            // Yaw rather than the forward vector, which has no heading left in it looking straight down
            float facing = Mathf.Repeat(view.transform.eulerAngles.y, 360f);

            for (int i = 0; i < marks.Length; i++)
            {
                float offset = Mathf.DeltaAngle(facing, i * Step);
                marks[i].anchoredPosition = new Vector2(Width * 0.5f + offset * Width / Span - MarkWidth * 0.5f, 0f);
            }

            Quests(facing);

            int degrees = Mathf.RoundToInt(facing) % 360;
            if (degrees == shown)
                return;

            shown = degrees;
            readout.text = $"{Points[Mathf.RoundToInt(facing / 45f) % 8]}  {degrees:000}°";
        }
    }
}
