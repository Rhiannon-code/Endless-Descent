using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.World;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // Draws whatever the place you are standing in reports about itself. Dungeons and settlements
    // are both drawn here because both describe themselves as footprints (IMapSource)
    [DisallowMultipleComponent]
    public class LocalMapPage : MonoBehaviour, IMenuPage
    {
        [SerializeField] PlaceLoader place;
        [SerializeField] Transform player;
        [SerializeField] RectTransform root;
        [SerializeField] Vector2 canvasSize = new Vector2(760f, 400f);

        readonly List<MapMarker> markers = new List<MapMarker>();
        readonly List<GameObject> drawn = new List<GameObject>();

        RectTransform surface;
        RectTransform content;
        MapPanZoom view;
        RectTransform legend;
        Text header;
        Text footer;
        int level;

        public string Title => "Local Map";
        public RectTransform Root => root;

        void Awake()
        {
            if (root == null)
                return;

            header = MenuWidgets.Label(root, new Vector2(20f, -14f), 760f, string.Empty, 16);
            header.color = new Color(0.95f, 0.85f, 0.5f);

            surface = MenuWidgets.Panel(root, "Surface", new Vector2(20f, -46f), canvasSize);
            surface.gameObject.AddComponent<RectMask2D>();

            // MenuWidgets.Block is never a raycast target, so a backdrop made of one cannot be
            // dragged. The map needs something under the pointer to catch the drag
            GameObject back = new GameObject("Backdrop", typeof(RectTransform));
            back.transform.SetParent(surface, false);
            Image backdrop = back.AddComponent<Image>();
            backdrop.color = new Color(0.03f, 0.03f, 0.05f, 0.9f);
            MenuWidgets.Anchor(backdrop.rectTransform, Vector2.zero, canvasSize);

            view = back.AddComponent<MapPanZoom>();
            view.Changed = Refresh;
            view.Moved = () => { if (content != null) content.anchoredPosition = view.Pan; };

            content = MenuWidgets.Panel(surface, "Content", Vector2.zero, canvasSize);

            MenuWidgets.Button(root, new Vector2(20f, -456f), new Vector2(34f, 24f), () => view.Step(1.4f)).text = "+";
            MenuWidgets.Button(root, new Vector2(58f, -456f), new Vector2(34f, 24f), () => view.Step(1f / 1.4f)).text = "-";
            MenuWidgets.Button(root, new Vector2(96f, -456f), new Vector2(64f, 24f), () => view.Reset()).text = "Fit";

            MenuWidgets.Button(root, new Vector2(20f, -456f), new Vector2(110f, 24f), () => Step(-1)).text = "  < Level";
            MenuWidgets.Button(root, new Vector2(140f, -456f), new Vector2(110f, 24f), () => Step(1)).text = "  Level >";

            footer = MenuWidgets.Label(root, new Vector2(266f, -456f), 520f, string.Empty);
            footer.color = new Color(0.72f, 0.78f, 0.9f);

            // The buildings are colour coded and the colours mean nothing without this
            legend = MenuWidgets.Panel(root, "Legend", new Vector2(20f, -486f), new Vector2(800f, 30f));
        }

        void Step(int delta)
        {
            IMapSource source = place != null ? place.Map : null;
            int levels = source != null ? Mathf.Max(1, source.LevelCount) : 1;

            level = Mathf.Clamp(level + delta, 0, levels - 1);
            Refresh();
        }

        public void Refresh()
        {
            foreach (GameObject item in drawn)
                Destroy(item);

            drawn.Clear();
            markers.Clear();
            Legend(null);

            IMapSource source = place != null ? place.Map : null;

            if (source == null || surface == null)
            {
                if (header != null) header.text = "LOCAL MAP: nowhere";
                return;
            }

            source.CollectMarkers(markers);
            header.text = $"{source.PlaceName.ToUpperInvariant()}  :  level {level + 1} of {Mathf.Max(1, source.LevelCount)}";

            if (markers.Count == 0)
            {
                footer.text = "Nothing mapped here yet.";
                return;
            }

            Rect extent = Extent();
            float scale = Mathf.Min(canvasSize.x / Mathf.Max(1f, extent.width),
                canvasSize.y / Mathf.Max(1f, extent.height)) * (view != null ? view.Zoom : 1f);

            foreach (MapMarker marker in markers)
            {
                if (marker.Level != level)
                    continue;

                Vector2 size = Vector2.Max(marker.Size * scale, Vector2.one * 2f);

                // A street runs at whatever angle it runs at, drawing it axis aligned turned the
                // map into a pile of grey bars that matched nothing on the ground
                Draw(Place(marker.Centre, extent, scale) - size * 0.5f, size, Tint(marker), marker.Rotation);
            }

            if (player != null)
            {
                Vector2 you = Place(new Vector2(player.position.x, player.position.z), extent, scale);
                Draw(you - Vector2.one * 5f, Vector2.one * 10f, new Color(1f, 0.85f, 0.2f));
            }

            footer.text = $"{markers.Count} features: yellow is you";
            Legend(markers);
        }

        // Only the trades actually present, so a hamlet's key is short and a city's is not a guess
        void Legend(List<MapMarker> from)
        {
            if (legend == null)
                return;

            foreach (Transform child in legend)
                Destroy(child.gameObject);

            if (from == null)
                return;

            List<int> seen = new List<int>();

            foreach (MapMarker marker in from)
            {
                if (marker.Kind != MapMarkerKind.Building || marker.Level != level)
                    continue;

                if (!seen.Contains(marker.Purpose))
                    seen.Add(marker.Purpose);
            }

            seen.Sort();

            float x = 0f;
            float y = 0f;

            foreach (int purpose in seen)
            {
                string label = ((RoomPurpose)purpose).ToString();
                float width = 22f + label.Length * 6.2f;

                if (x + width > 796f)
                {
                    x = 0f;
                    y -= 15f;
                }

                MenuWidgets.Block(legend, new Vector2(x, y + 3f), new Vector2(9f, 9f),
                    Color.HSVToRGB(purpose * 0.6180339887f % 1f, 0.5f, 0.82f));

                Text caption = MenuWidgets.Label(legend, new Vector2(x + 13f, y), width - 13f, label, 10);
                caption.color = new Color(0.78f, 0.78f, 0.78f);

                x += width;
            }
        }

        // Screen space runs down from the top left, world Z runs up, so the vertical axis flips
        // Centred rather than corner anchored, so zooming holds the middle of the view instead of
        // sliding everything off the top left
        Vector2 Place(Vector2 world, Rect extent, float scale)
        {
            // Pan is applied by moving the content, not by shifting every marker
            return new Vector2(
                (world.x - extent.center.x) * scale + canvasSize.x * 0.5f,
                -((extent.center.y - world.y) * scale + canvasSize.y * 0.5f));
        }

        void Draw(Vector2 position, Vector2 size, Color colour, float rotation = 0f) =>
            drawn.Add(MenuWidgets.Block(content, position, size, colour, rotation).gameObject);

        Rect Extent()
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            foreach (MapMarker marker in markers)
            {
                min = Vector2.Min(min, marker.Centre - marker.Size * 0.5f);
                max = Vector2.Max(max, marker.Centre + marker.Size * 0.5f);
            }

            Vector2 pad = Vector2.one * 4f;
            return new Rect(min - pad, Vector2.Max(max - min + pad * 2f, Vector2.one));
        }

        // A building is drawn in the same colour its walls are, so the map doubles as the legend
        static Color Tint(MapMarker marker)
        {
            if (marker.Kind == MapMarkerKind.Building)
                return Color.HSVToRGB(marker.Purpose * 0.6180339887f % 1f, 0.5f, 0.82f);

            switch (marker.Kind)
            {
                case MapMarkerKind.Street: return new Color(0.42f, 0.40f, 0.36f);
                case MapMarkerKind.Square: return new Color(0.56f, 0.52f, 0.42f);
                case MapMarkerKind.Building: return new Color(0.68f, 0.60f, 0.46f);
                case MapMarkerKind.Water: return new Color(0.20f, 0.34f, 0.50f);
                case MapMarkerKind.Corridor: return new Color(0.32f, 0.34f, 0.40f);
                case MapMarkerKind.Entrance: return new Color(0.45f, 0.85f, 0.55f);
                case MapMarkerKind.Boss: return new Color(0.85f, 0.35f, 0.35f);
                default: return new Color(0.55f, 0.58f, 0.66f);
            }
        }
    }
}
