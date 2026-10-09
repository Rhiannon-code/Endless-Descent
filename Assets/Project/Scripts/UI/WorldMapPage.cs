using System.Collections.Generic;
using EndlessDescent.Data;
using EndlessDescent.Items;
using EndlessDescent.Quests;
using EndlessDescent.World;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // The empire drawn where it actually is, at whatever zoom you want it. Seventy one places will
    // not label themselves legibly at one scale, so you move the map instead and a label is dropped
    // only when it would sit on one already drawn
    [DisallowMultipleComponent]
    public class WorldMapPage : MonoBehaviour, IMenuPage
    {
        [SerializeField] PlaceLoader place;
        [SerializeField] GameMenu menu;
        [SerializeField] Journey journey;
        [SerializeField] QuestJournal journal;
        [SerializeField] Wallet wallet;
        [SerializeField] RectTransform root;
        [SerializeField] Vector2 canvasSize = new Vector2(520f, 400f);
        [SerializeField] Sprite mapImage;

        const float WalkSpeed = 40f;
        const float RunSpeed = 300f;

        readonly List<GameObject> drawn = new List<GameObject>();
        readonly List<Rect> claimed = new List<Rect>();

        RectTransform surface;
        RectTransform content;
        RectTransform panel;
        Image sheet;
        Image you;
        Rect mapExtent;
        float mapScale;
        Vector2 mapOrigin;
        MapPanZoom view;
        Text header;
        Text summary;

        int chosen = -1;
        TravelPace pace = TravelPace.Cautious;
        TravelLodging lodging = TravelLodging.Inn;
        TravelWay way = TravelWay.Paved;

        public string Title => "World Map";
        public RectTransform Root => root;

        void Awake()
        {
            if (root == null)
                return;

            header = MenuWidgets.Label(root, new Vector2(20f, -14f), 760f, string.Empty, 16);
            header.color = new Color(0.95f, 0.85f, 0.5f);

            surface = MenuWidgets.Panel(root, "Surface", new Vector2(20f, -46f), canvasSize);
            surface.gameObject.AddComponent<RectMask2D>();

            // The backdrop carries the dragging, not the surface. Panning has to work on empty map,
            // and a drag handler on an ancestor of the markers steals the click that selects one
            GameObject back = new GameObject("Backdrop", typeof(RectTransform));
            back.transform.SetParent(surface, false);
            Image backdrop = back.AddComponent<Image>();
            backdrop.color = new Color(0.06f, 0.08f, 0.07f, 0.92f);
            MenuWidgets.Anchor(backdrop.rectTransform, Vector2.zero, canvasSize);

            view = back.AddComponent<MapPanZoom>();
            view.Changed = Redraw;
            view.Moved = Move;

            content = MenuWidgets.Panel(surface, "Content", Vector2.zero, canvasSize);

            sheet = MenuWidgets.Block(content, Vector2.zero, canvasSize, Color.white);
            sheet.sprite = mapImage;
            sheet.enabled = mapImage != null;
            sheet.type = Image.Type.Simple;

            MenuWidgets.Button(root, new Vector2(20f, -456f), new Vector2(34f, 24f), () => view.Step(1.4f)).text = "+";
            MenuWidgets.Button(root, new Vector2(58f, -456f), new Vector2(34f, 24f), () => view.Step(1f / 1.4f)).text = "-";
            MenuWidgets.Button(root, new Vector2(96f, -456f), new Vector2(64f, 24f), () => view.Reset()).text = "Fit";

            MenuWidgets.Label(root, new Vector2(168f, -456f), 400f,
                "drag to pan, wheel to zoom", 11).color = new Color(0.6f, 0.6f, 0.6f);

            // Kept out of the redraw list, it has to survive a rebuild and be movable every frame
            // while a journey is running
            you = MenuWidgets.Block(content, Vector2.zero, new Vector2(13f, 13f),
                new Color(1f, 0.95f, 0.35f, 1f));

            panel = MenuWidgets.Panel(root, "Journey", new Vector2(560f, -46f), new Vector2(240f, 400f));
        }

        public void Refresh()
        {
            if (journal == null)
                journal = FindFirstObjectByType<QuestJournal>();

            Redraw();
        }

        static int IndexOf(RegionMap region, string displayName)
        {
            for (int i = 0; i < region.Locations.Count; i++)
                if (region.Locations[i].DisplayName == displayName) return i;

            return -1;
        }

        // Panning moves what is drawn rather than drawing it again, rebuilding every marker on every
        // drag frame is what made a marker vanish between the press and the release
        void Move()
        {
            if (content != null)
                content.anchoredPosition = view.Pan;
        }

        void Redraw()
        {
            foreach (GameObject item in drawn)
                Destroy(item);

            drawn.Clear();
            claimed.Clear();

            RegionMap region = place != null ? place.Region : null;

            if (region == null || surface == null)
            {
                if (header != null) header.text = "WORLD MAP: no region";
                return;
            }

            int here = place.Current;

            // Anywhere a quest is pointing, so the map answers "where now" at a glance
            HashSet<int> wanted = new HashSet<int>();

            if (journal != null)
            {
                foreach (Quest quest in journal.Active)
                {
                    if (QuestDirections.TryFind(quest, region, place.HereKm, out QuestDirections.Heading heading))
                        wanted.Add(IndexOf(region, heading.Place));
                }
            }

            IReadOnlyList<RegionLocation> locations = region.Locations;

            Vector2 standing = place.HereKm;
            header.text = $"THE ORDOVAN EMPIRE: {Nearest(locations, standing)}   (x{view.Zoom:0.0})";

            // The same rectangle the exporter painted, so the markers land on their own coastlines
            Rect extent = WorldMapView.Bounds(WorldGeography.Ordovan());
            float scale = Mathf.Min(canvasSize.x / extent.width, canvasSize.y / extent.height) * view.Zoom;

            Vector2 sheetSize = new Vector2(extent.width, extent.height) * scale;
            Vector2 origin = (canvasSize - sheetSize) * 0.5f;

            mapExtent = extent;
            mapScale = scale;
            mapOrigin = origin;

            if (sheet != null)
            {
                sheet.enabled = mapImage != null;
                MenuWidgets.Anchor(sheet.rectTransform, new Vector2(origin.x, -origin.y), sheetSize);
            }

            Move();

            // Labels first and markers second, in two passes. Drawn together, a name landed on top
            // of every marker to its left and took the click meant for it
            List<int> order = ByRank(locations);
            List<Vector2> points = new List<Vector2>(order.Count);

            foreach (int i in order)
            {
                Vector2 fraction = WorldMapView.Fraction(extent, locations[i].Position);

                points.Add(new Vector2(
                    origin.x + fraction.x * sheetSize.x,
                    -(origin.y + (1f - fraction.y) * sheetSize.y)));
            }

            // Biggest first, so when two labels collide it is the hamlet that goes and not the city
            for (int n = 0; n < order.Count; n++)
                Label(points[n], locations[order[n]].DisplayName, order[n] == here);

            for (int n = 0; n < order.Count; n++)
            {
                int i = order[n];
                RegionLocation location = locations[i];
                bool current = i == here;

                // Worth hitting. Seven pixels was a hard target even before the labels were eating
                // the clicks
                float size = current ? 20f : location.IsSettlement ? 18f : 15f;
                Color colour = current ? new Color(1f, 0.85f, 0.2f)
                    : wanted.Contains(i) ? new Color(0.45f, 0.85f, 1f)
                    : location.IsSettlement ? new Color(0.72f, 0.66f, 0.48f) : new Color(0.62f, 0.42f, 0.40f);

                int captured = i;
                Text dot = MenuWidgets.Button(content, points[n] - Vector2.one * (size * 0.5f),
                    Vector2.one * size, () => Choose(captured), colour);
                dot.text = string.Empty;
                drawn.Add(dot.transform.parent.gameObject);
            }

            // The route you are on, or the one you are looking at, laid over the map as a dotted run
            if (journey != null && journey.Travelling)
                Trail(journey.Path);
            else if (chosen >= 0 && chosen != here)
                Trail(Plan(region, place.HereKm, chosen).Path);

            You();
            DrawJourney(region, here);
        }

        // Where you actually are, which during a journey is somewhere between two places and was
        // previously not shown at all
        void You()
        {
            if (you == null || place == null)
                return;

            you.transform.SetAsLastSibling();
            MenuWidgets.Anchor(you.rectTransform, Point(place.HereKm) - Vector2.one * 6.5f,
                new Vector2(13f, 13f));
        }

        Vector2 Point(Vector2 km)
        {
            Vector2 fraction = WorldMapView.Fraction(mapExtent, km);

            return new Vector2(
                mapOrigin.x + fraction.x * mapExtent.width * mapScale,
                -(mapOrigin.y + (1f - fraction.y) * mapExtent.height * mapScale));
        }

        void Trail(Vector2[] path)
        {
            if (path == null || path.Length < 2)
                return;

            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 a = Point(path[i]);
                Vector2 b = Point(path[i + 1]);

                for (int step = 0; step <= 4; step++)
                {
                    Vector2 at = Vector2.Lerp(a, b, step / 4f);

                    drawn.Add(MenuWidgets.Block(content, at - Vector2.one * 1.5f, Vector2.one * 3f,
                        new Color(1f, 0.85f, 0.4f, 0.75f)).gameObject);
                }
            }
        }

        TravelPlan Plan(RegionMap region, Vector2 from, int to) =>
            TravelPlan.For(place.World != null ? place.World.Surface : null, from,
                region.Locations[to].Position, pace, lodging, way);

        // A journey moves the player for minutes at a time. The marker follows without redrawing the
        // map, which at seventy one markers a frame would be unaffordable
        void Update()
        {
            if (journey != null && journey.Travelling && root != null && root.gameObject.activeInHierarchy)
                You();
        }

        static string Nearest(IReadOnlyList<RegionLocation> locations, Vector2 km)
        {
            string best = "in open country";
            float span = float.MaxValue;

            foreach (RegionLocation location in locations)
            {
                float d = Vector2.Distance(location.Position, km);

                if (d >= span) continue;

                span = d;
                best = d < 1f ? $"you are at {location.DisplayName}" : $"{d:0} km from {location.DisplayName}";
            }

            return best;
        }

        // A label is drawn unless its box lands on one already taken. Only the name is dropped; the
        // marker always stays, so a place is never invisible, only unlabelled
        void Label(Vector2 point, string name, bool current)
        {
            float width = name.Length * 5.4f;

            // Four places to try before giving up on a name. Only ever offering the right hand side
            // threw away most of the map's labels for want of somewhere else to put them
            Vector2[] tries =
            {
                new Vector2(11f, 0f),
                new Vector2(-11f - width, 0f),
                new Vector2(-width * 0.5f, 15f),
                new Vector2(-width * 0.5f, -15f)
            };

            Vector2 at = Vector2.zero;
            bool room = false;

            foreach (Vector2 offset in tries)
            {
                Vector2 candidate = point + offset;
                Rect box = new Rect(candidate.x, candidate.y - 15f, width, 16f);
                bool clash = false;

                foreach (Rect taken in claimed)
                    if (taken.Overlaps(box)) { clash = true; break; }

                if (clash) continue;

                claimed.Add(box);
                at = candidate;
                room = true;
                break;
            }

            if (!room)
                return;

            Text pin = MenuWidgets.Label(content, at, 160f, name, 10);
            pin.color = current ? new Color(1f, 0.9f, 0.6f) : new Color(0.75f, 0.75f, 0.75f);

            drawn.Add(pin.gameObject);
        }

        static List<int> ByRank(IReadOnlyList<RegionLocation> locations)
        {
            Dictionary<string, int> rank = new Dictionary<string, int>();

            foreach (OrdovanPlaces.Place p in OrdovanPlaces.All)
                rank[p.Name] = p.IsSettlement ? 10 - (int)p.Tier : 20;

            List<int> order = new List<int>();

            for (int i = 0; i < locations.Count; i++)
                order.Add(i);

            order.Sort((a, b) =>
            {
                rank.TryGetValue(locations[a].DisplayName, out int ra);
                rank.TryGetValue(locations[b].DisplayName, out int rb);
                return ra != rb ? ra.CompareTo(rb) : a.CompareTo(b);
            });

            return order;
        }

        void Choose(int index)
        {
            chosen = index;
            Redraw();
        }

        void DrawJourney(RegionMap region, int here)
        {
            Vector2 from = place.HereKm;

            if (chosen < 0 || chosen == here
                || Vector2.Distance(from, region.Locations[chosen].Position) < 1f)
            {
                summary = MenuWidgets.Label(panel, new Vector2(8f, -10f), 224f,
                    chosen < 0 ? "Pick a place on the map." : "You are already here.", 11);
                summary.color = new Color(0.65f, 0.65f, 0.65f);
                drawn.Add(summary.gameObject);
                return;
            }

            TravelPlan plan = Plan(region, from, chosen);

            Text title = MenuWidgets.Label(panel, new Vector2(8f, -10f), 224f,
                region.Locations[chosen].DisplayName, 13);
            title.color = new Color(0.95f, 0.85f, 0.5f);
            drawn.Add(title.gameObject);

            Text terms = MenuWidgets.Label(panel, new Vector2(8f, -32f), 224f, plan.Describe(), 11);
            drawn.Add(terms.gameObject);

            drawn.Add(Toggle(new Vector2(8f, -60f),
                pace == TravelPace.Cautious ? "Cautiously" : "Recklessly",
                () => { pace = pace == TravelPace.Cautious ? TravelPace.Reckless : TravelPace.Cautious; Redraw(); }));

            drawn.Add(Toggle(new Vector2(8f, -88f),
                lodging == TravelLodging.Inn ? "Rest at inns" : "Camp out",
                () => { lodging = lodging == TravelLodging.Inn ? TravelLodging.Camping : TravelLodging.Inn; Redraw(); }));

            drawn.Add(Toggle(new Vector2(8f, -116f),
                way == TravelWay.Paved ? "By paved road" : way == TravelWay.Dirt ? "By dirt track" : "Direct",
                () => { way = way == TravelWay.Paved ? TravelWay.Dirt
                    : way == TravelWay.Dirt ? TravelWay.Direct : TravelWay.Paved; Redraw(); }));

            bool affordable = plan.Gold <= 0 || wallet == null || wallet.CanAfford(plan.Gold);

            if (!affordable)
            {
                Text warn = MenuWidgets.Label(panel, new Vector2(8f, -144f), 224f,
                    $"You cannot afford {plan.Gold} gold in lodging.", 11);
                warn.color = new Color(1f, 0.6f, 0.55f);
                drawn.Add(warn.gameObject);
                return;
            }

            drawn.Add(Go(new Vector2(8f, -148f), "Arrive instantly", plan, 0f));
            drawn.Add(Go(new Vector2(8f, -178f), $"Travel  ({WalkSpeed:0}x)", plan, WalkSpeed));
            drawn.Add(Go(new Vector2(8f, -208f), $"Travel  ({RunSpeed:0}x)", plan, RunSpeed));
        }

        GameObject Toggle(Vector2 at, string label, System.Action onClick)
        {
            Text row = MenuWidgets.Button(panel, at, new Vector2(224f, 24f), onClick);
            row.text = label;
            row.fontSize = 11;
            return row.transform.parent.gameObject;
        }

        GameObject Go(Vector2 at, string label, TravelPlan plan, float speed)
        {
            int destination = chosen;

            Text row = MenuWidgets.Button(panel, at, new Vector2(224f, 26f),
                () => Depart(destination, plan, speed), new Color(0.16f, 0.20f, 0.16f, 0.95f));

            row.text = label;
            row.fontSize = 11;
            return row.transform.parent.gameObject;
        }

        void Depart(int destination, TravelPlan plan, float speed)
        {
            if (plan.Gold > 0 && wallet != null && !wallet.TrySpend(plan.Gold))
                return;

            menu?.Close();

            // Walking has already spent the time by the time it arrives, so arrival costs no hours
            if (speed > 0f && journey != null)
            {
                void OnArrived()
                {
                    journey.Arrived -= OnArrived;

                    if (journey.Finished)
                        place.TravelTo(destination, 0f);
                }

                journey.Arrived += OnArrived;
                journey.Begin(plan, speed);
                return;
            }

            place.TravelTo(destination, plan.Hours);
        }
    }
}
