using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Dungeons
{
    [DisallowMultipleComponent]
    public class DungeonVisibility : MonoBehaviour
    {
        [SerializeField] DungeonGenerator generator;
        [SerializeField] Transform viewer;
        [SerializeField, Min(0)] int roomDepth = 4;
        [SerializeField, Min(0)] int lightDepth = 2;
        [SerializeField, Min(0.05f)] float interval = 0.2f;

        sealed class Cell
        {
            // Gathered again whenever the room is shown or hidden rather than once at build. The
            // creatures, loot and furniture put into a room after it is built were never in the list,
            // so they drew everywhere, through the floor of the surface above included
            public Transform Room;

            // The room's own box and one for each corridor it owns. Only the room was tested, so
            // halfway down a long corridor the player was in nothing and fell back to whatever room
            // was nearest in a straight line, which is often the one a storey below
            public readonly List<Bounds> Boxes = new List<Bounds>();
            public int Depth = -1;
            public bool Drawn = true;
            public bool Lit = true;
        }

        // Feet stand exactly on a floor, and a floor is the top of the storey below, so the test point
        // is at the chest, inside one storey and clear of the next
        const float ProbeHeight = 1f;

        readonly Dictionary<int, Cell> cells = new Dictionary<int, Cell>();
        readonly Queue<int> frontier = new Queue<int>();
        readonly List<int> found = new List<int>();

        // Every room whose space the player is in. Boxes overlap at doorways and in the bands between
        // cells where corridors and stairs run, and betting on one of them is how a room through the
        // floor won and everything around the player went dark. All of them count
        readonly HashSet<int> current = new HashSet<int>();

        DungeonBuildResult indexed;
        float nextCheck;

        public int VisibleRooms { get; private set; }
        public IReadOnlyCollection<int> Rooms => current;

        void OnEnable()
        {
            WorldOrigin.Shifted += OnShifted;
            CurrentSpace.Changed += OnSpaceChanged;
        }

        void OnDisable()
        {
            WorldOrigin.Shifted -= OnShifted;
            CurrentSpace.Changed -= OnSpaceChanged;
        }

        // The room volumes were taken where the dungeon stood, and the whole scene has just moved
        void OnShifted(Vector3 by)
        {
            foreach (Cell cell in cells.Values)
            {
                for (int i = 0; i < cell.Boxes.Count; i++)
                {
                    Bounds box = cell.Boxes[i];
                    box.center -= by;
                    cell.Boxes[i] = box;
                }
            }
        }

        void OnSpaceChanged(SpaceKind kind)
        {
            if (kind == SpaceKind.Dungeon)
            {
                current.Clear();
                nextCheck = 0f;
                return;
            }

            HideAll();
        }

        // A dungeon prepared under its entrance is still in front of the camera from above, and every
        // room of it would draw while the player is standing in the daylight over it
        void HideAll()
        {
            foreach (Cell cell in cells.Values)
                Show(cell, false, false);

            current.Clear();
            VisibleRooms = 0;
        }

        void LateUpdate()
        {
            if (generator == null || viewer == null)
                return;

            if (!ReferenceEquals(generator.Current, indexed))
                Index();

            if (CurrentSpace.Kind != SpaceKind.Dungeon)
                return;

            if (Time.time < nextCheck)
                return;

            nextCheck = Time.time + interval;

            Vector3 probe = viewer.position + Vector3.up * ProbeHeight;
            Locate(probe);

            // In a gap between boxes the room just left is still the best guess, it is always next
            // door. Only with nothing to go on does the nearest room by distance get a say
            if (found.Count == 0)
            {
                if (current.Count > 0)
                    return;

                int nearest = Nearest(probe);
                if (nearest < 0)
                    return;

                found.Add(nearest);
            }

            if (current.SetEquals(found))
                return;

            current.Clear();
            current.UnionWith(found);
            Apply();
        }

        void Index()
        {
            cells.Clear();
            current.Clear();
            indexed = generator.Current;

            if (indexed == null)
                return;

            Dictionary<Transform, Cell> byRoom = new Dictionary<Transform, Cell>();

            foreach (KeyValuePair<int, GameObject> pair in indexed.Rooms)
            {
                if (pair.Value == null)
                    continue;

                RoomModule module = pair.Value.GetComponent<RoomModule>();

                Cell cell = new Cell { Room = pair.Value.transform };

                cell.Boxes.Add(WorldBounds(pair.Value.transform, module));
                cells[pair.Key] = cell;
                byRoom[pair.Value.transform] = cell;
            }

            foreach (GameObject corridor in indexed.Corridors)
            {
                if (corridor != null && corridor.transform.parent != null &&
                    byRoom.TryGetValue(corridor.transform.parent, out Cell owner) && TryBox(corridor, out Bounds box))
                    owner.Boxes.Add(box);
            }

            if (CurrentSpace.Kind != SpaceKind.Dungeon)
                HideAll();
        }

        static Bounds WorldBounds(Transform room, RoomModule module)
        {
            Bounds local = module != null ? module.LocalBounds : new Bounds(Vector3.up * 2f, new Vector3(8f, 4f, 8f));

            // Widened a metre each way to take in its doorways, and never upwards or downwards, where
            // the extra metre reached into the storey below
            Bounds bounds = new Bounds(room.TransformPoint(local.center), local.size);
            bounds.Expand(new Vector3(2f, 0f, 2f));
            return bounds;
        }

        // From the meshes rather than Renderer.bounds, which depends on the renderer being on, and
        // culling is exactly what switches them off
        static bool TryBox(GameObject piece, out Bounds box)
        {
            box = default;
            bool any = false;

            foreach (MeshFilter filter in piece.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;

                Bounds local = filter.sharedMesh.bounds;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = filter.transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)));

                    if (any) box.Encapsulate(point);
                    else box = new Bounds(point, Vector3.zero);

                    any = true;
                }
            }

            if (any)
                box.Expand(new Vector3(1f, 0f, 1f));

            return any;
        }

        static bool Contains(Cell cell, Vector3 position)
        {
            foreach (Bounds box in cell.Boxes)
                if (box.Contains(position)) return true;

            return false;
        }

        void Locate(Vector3 position)
        {
            found.Clear();

            foreach (KeyValuePair<int, Cell> pair in cells)
                if (Contains(pair.Value, position)) found.Add(pair.Key);
        }

        int Nearest(Vector3 position)
        {
            int nearest = -1;
            float best = float.MaxValue;

            foreach (KeyValuePair<int, Cell> pair in cells)
            {
                foreach (Bounds box in pair.Value.Boxes)
                {
                    float distance = Apart(box, position);
                    if (distance >= best)
                        continue;

                    best = distance;
                    nearest = pair.Key;
                }
            }

            return nearest;
        }

        // A storey is 5 m and a corridor up to 31, so in a straight line the room through the floor is
        // often nearer than the one at the far end of the corridor. Height counts four times over
        static float Apart(Bounds box, Vector3 position)
        {
            Vector3 gap = box.ClosestPoint(position) - position;
            gap.y *= 4f;
            return gap.sqrMagnitude;
        }

        // Breadth first over the spanning tree, because those are the edges that actually have
        // corridors built for them, a non tree edge is a line on the graph and nothing in the world
        void Apply()
        {
            DungeonGraph graph = generator.Graph;
            if (graph == null)
                return;

            foreach (Cell cell in cells.Values)
                cell.Depth = -1;

            frontier.Clear();

            foreach (int from in current)
            {
                if (!cells.TryGetValue(from, out Cell start))
                    continue;

                start.Depth = 0;
                frontier.Enqueue(from);
            }

            int reach = Mathf.Max(roomDepth, lightDepth);

            while (frontier.Count > 0)
            {
                int id = frontier.Dequeue();
                int depth = cells[id].Depth;

                if (depth >= reach)
                    continue;

                foreach (int edgeId in graph.Node(id).EdgeIds)
                {
                    DungeonEdge edge = graph.Edge(edgeId);
                    if (!edge.IsSpanningTree)
                        continue;

                    int next = graph.Opposite(edge, id);

                    if (!cells.TryGetValue(next, out Cell cell) || cell.Depth >= 0)
                        continue;

                    cell.Depth = depth + 1;
                    frontier.Enqueue(next);
                }
            }

            VisibleRooms = 0;

            foreach (Cell cell in cells.Values)
            {
                bool draw = cell.Depth >= 0 && cell.Depth <= roomDepth;
                bool lit = cell.Depth >= 0 && cell.Depth <= lightDepth;

                if (draw)
                    VisibleRooms++;

                Show(cell, draw, lit);
            }
        }

        // After a floor is filled, what was just put into its rooms takes on their current state
        public void Restate()
        {
            foreach (Cell cell in cells.Values)
                Show(cell, cell.Drawn, cell.Lit, true);
        }

        // Renderers and lights only. Colliders stay on, so a culled room is still solid, spells still
        // hit its walls, and anything living in it keeps thinking
        static void Show(Cell cell, bool draw, bool lit, bool force = false)
        {
            if (cell.Room == null)
                return;

            if (force || cell.Drawn != draw)
            {
                foreach (Renderer renderer in cell.Room.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = draw;

                cell.Drawn = draw;
            }

            if (!force && cell.Lit == lit)
                return;

            foreach (Light light in cell.Room.GetComponentsInChildren<Light>(true))
                light.enabled = lit;

            cell.Lit = lit;
        }
    }
}
