using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    public sealed class WorldStreams
    {
        public readonly struct Hit
        {
            public readonly float DistanceKm;
            public readonly float DepthMetres;
            public readonly float WidthKm;
            public readonly Vector2 Point;

            public bool Found => WidthKm > 0f;

            public Hit(float distanceKm, float depthMetres, float widthKm, Vector2 point)
            {
                DistanceKm = distanceKm;
                DepthMetres = depthMetres;
                WidthKm = widthKm;
                Point = point;
            }
        }

        struct Segment
        {
            public Vector2 A;
            public Vector2 B;
            public float Depth;
            public float Width;
        }

        const float CellKm = 1f;
        const float LookupCellKm = 2f;

        // How much land must drain through a place before it counts as running water. This is the
        // dial that decides whether the world has rivers or is covered in them
        const float CatchmentKm2 = 95f;
        const float BigCatchmentKm2 = 900f;

        const float SourceDepth = 1.2f;
        const float MouthDepth = 9f;
        const float SourceWidth = 0.012f;
        const float MouthWidth = 0.055f;

        const float LakeDepthMetres = 6f;
        const float LakePoolKm2 = 120f;
        const int MinLakeCells = 4;
        const int MaxLakes = 18;
        const float MaxLakeDepthMetres = 16f;

        readonly List<Segment> segments = new List<Segment>();
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();

        // Standing water, keyed by the same one kilometre cell the flood was computed on. A lake is
        // not a separate idea here, the priority flood already had to decide how deep a basin fills
        // before it spills, and that answer is the lake
        readonly Dictionary<long, float> lakes = new Dictionary<long, float>();

        public int Count => segments.Count;
        public float LengthKm { get; private set; }
        public int Outlets { get; private set; }

        // Read-only views, for anything that needs to draw the network rather than query it
        public IEnumerable<(Vector2 A, Vector2 B, float Width)> EachSegment()
        {
            foreach (Segment segment in segments)
                yield return (segment.A, segment.B, segment.Width);
        }

        public IEnumerable<(Vector2 Km, float Level)> EachLakeCell()
        {
            foreach (KeyValuePair<long, float> cell in lakes)
            {
                int x = (int)(cell.Key >> 32);
                int y = (int)(uint)(cell.Key & 0xFFFFFFFF);
                yield return (new Vector2((x + 0.5f) * CellKm, (y + 0.5f) * CellKm), cell.Value);
            }
        }
        public int Lakes { get; private set; }
        public int LakeCells => lakes.Count;
        public float LakeAreaKm2 => lakes.Count * CellKm * CellKm;

        // The level standing water reaches here, or zero if the ground is dry
        public float WaterLevel(Vector2 km)
        {
            long key = Key(Mathf.FloorToInt(km.x / CellKm), Mathf.FloorToInt(km.y / CellKm));
            return lakes.TryGetValue(key, out float level) ? level : 0f;
        }

        public static WorldStreams Trace(WorldGeography geography, Rect area,
            System.Func<Vector2, float> bare, System.Func<Vector2, float> trunkKm, int seed)
        {
            WorldStreams streams = new WorldStreams();

            int nx = Mathf.Max(2, Mathf.CeilToInt(area.width / CellKm));
            int ny = Mathf.Max(2, Mathf.CeilToInt(area.height / CellKm));
            int count = nx * ny;

            float[] height = new float[count];
            float[] filled = new float[count];
            int[] drainsTo = new int[count];
            bool[] settled = new bool[count];

            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
                height[y * nx + x] = bare(Centre(area, x, y));

            // Priority flood, outward from the sea and the edges of the world. Taking the lowest
            // unsettled cell each time means water leaves a basin over its lowest lip, which is what
            // a lake does before it overflows, and no cell is left with nowhere to go
            Heap open = new Heap(count);

            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
            {
                int i = y * nx + x;
                bool edge = x == 0 || y == 0 || x == nx - 1 || y == ny - 1;

                if (!edge && height[i] > 0f)
                    continue;

                filled[i] = height[i];
                drainsTo[i] = -1;
                settled[i] = true;
                open.Push(i, filled[i]);
            }

            int[] neighbours = new int[8];

            while (open.Count > 0)
            {
                int at = open.Pop();
                int found = Around(at % nx, at / nx, nx, ny, neighbours);

                for (int n = 0; n < found; n++)
                {
                    int next = neighbours[n];

                    if (settled[next])
                        continue;

                    settled[next] = true;
                    filled[next] = Mathf.Max(height[next], filled[at]);
                    drainsTo[next] = at;
                    open.Push(next, filled[next]);
                }
            }

            // Upstream to downstream. Cells drained lowest first, so walking that order backwards
            // hands every cell its own catchment before it passes it on
            float[] flow = new float[count];
            int[] order = open.Drained;

            for (int i = 0; i < count; i++)
                flow[i] = CellKm * CellKm;

            for (int i = order.Length - 1; i >= 0; i--)
            {
                int to = drainsTo[order[i]];

                if (to >= 0)
                    flow[to] += flow[order[i]];
            }

            for (int i = 0; i < count; i++)
            {
                int to = drainsTo[i];

                Vector2 here = Centre(area, i % nx, i / nx);

                if (to < 0)
                {
                    streams.Outlets++;
                    continue;
                }

                if (flow[i] < CatchmentKm2 || height[i] <= 0f)
                    continue;

                Vector2 a = here;

                // Clear of the two rivers the Survey actually drew: those carve themselves
                if (trunkKm(a) < 1.5f)
                    continue;

                float size = Mathf.Clamp01(Mathf.Log(flow[i] / CatchmentKm2 + 1f)
                                           / Mathf.Log(BigCatchmentKm2 / CatchmentKm2 + 1f));

                streams.Add(new Segment
                {
                    A = a,
                    B = Centre(area, to % nx, to / nx),
                    Depth = Mathf.Lerp(SourceDepth, MouthDepth, size),
                    Width = Mathf.Lerp(SourceWidth, MouthWidth, size)
                });
            }

            streams.Pool(area, nx, ny, height, filled, flow);
            return streams;
        }

        // Every depression the flood filled is standing water of a sort, and on this terrain that is
        // thousands of puddles covering an eighth of the land. A lake is a *body* of water, so the
        // flooded cells are grouped into connected bodies and only the largest few are kept, which
        // is also the only way to say "a few lakes" and mean it
        void Pool(Rect area, int nx, int ny, float[] height, float[] filled, float[] flow)
        {
            bool[] wet = new bool[height.Length];

            for (int i = 0; i < height.Length; i++)
                wet[i] = height[i] > 0f && filled[i] - height[i] > LakeDepthMetres && flow[i] > LakePoolKm2;

            bool[] seen = new bool[height.Length];
            List<List<int>> bodies = new List<List<int>>();
            Queue<int> open = new Queue<int>();
            int[] neighbours = new int[8];

            for (int start = 0; start < wet.Length; start++)
            {
                if (!wet[start] || seen[start]) continue;

                List<int> body = new List<int>();
                seen[start] = true;
                open.Enqueue(start);

                while (open.Count > 0)
                {
                    int at = open.Dequeue();
                    body.Add(at);

                    int found = Around(at % nx, at / nx, nx, ny, neighbours);

                    for (int n = 0; n < found; n++)
                    {
                        int next = neighbours[n];

                        if (!wet[next] || seen[next]) continue;

                        seen[next] = true;
                        open.Enqueue(next);
                    }
                }

                if (body.Count >= MinLakeCells)
                    bodies.Add(body);
            }

            bodies.Sort((a, b) => b.Count.CompareTo(a.Count));

            int kept = 0;

            for (int i = 0; i < bodies.Count && kept < MaxLakes; i++)
            {
                List<int> body = bodies[i];
                float floor = float.MaxValue;
                float spill = 0f;

                foreach (int cell in body)
                {
                    floor = Mathf.Min(floor, height[cell]);
                    spill = Mathf.Max(spill, filled[cell]);
                }

                // A basin here can be a hundred kilometres across and fill to the height of a
                // faraway lip, which would make one lake the size of a province. The water is held
                // to a believable depth over the deepest ground instead, and only what is under that
                // level is lake
                float level = Mathf.Min(spill, floor + MaxLakeDepthMetres);
                int wetCells = 0;

                foreach (int cell in body)
                {
                    if (height[cell] >= level) continue;

                    Vector2 at = Centre(area, cell % nx, cell / nx);
                    lakes[Key(Mathf.FloorToInt(at.x / CellKm), Mathf.FloorToInt(at.y / CellKm))] = level;
                    wetCells++;
                }

                if (wetCells >= MinLakeCells) kept++;
            }

            Lakes = kept;
        }

        static Vector2 Centre(Rect area, int x, int y) =>
            new Vector2(area.xMin + (x + 0.5f) * CellKm, area.yMin + (y + 0.5f) * CellKm);

        static int Around(int x, int y, int nx, int ny, int[] into)
        {
            int found = 0;

            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                int px = x + dx, py = y + dy;

                if (px < 0 || py < 0 || px >= nx || py >= ny) continue;

                into[found++] = py * nx + px;
            }

            return found;
        }

        void Add(Segment segment)
        {
            int index = segments.Count;
            segments.Add(segment);
            LengthKm += Vector2.Distance(segment.A, segment.B);

            int x0 = Cell(Mathf.Min(segment.A.x, segment.B.x)), x1 = Cell(Mathf.Max(segment.A.x, segment.B.x));
            int y0 = Cell(Mathf.Min(segment.A.y, segment.B.y)), y1 = Cell(Mathf.Max(segment.A.y, segment.B.y));

            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            {
                long key = Key(x, y);

                if (!grid.TryGetValue(key, out List<int> bucket))
                    grid[key] = bucket = new List<int>();

                bucket.Add(index);
            }
        }

        public Hit Nearest(Vector2 km)
        {
            int cx = Cell(km.x), cy = Cell(km.y);
            float best = float.MaxValue;
            int found = -1;
            Vector2 point = km;

            for (int x = cx - 1; x <= cx + 1; x++)
            for (int y = cy - 1; y <= cy + 1; y++)
            {
                if (!grid.TryGetValue(Key(x, y), out List<int> bucket))
                    continue;

                foreach (int index in bucket)
                {
                    Segment segment = segments[index];
                    Vector2 ab = segment.B - segment.A;
                    float span = ab.sqrMagnitude;
                    float t = span < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(km - segment.A, ab) / span);
                    Vector2 on = segment.A + ab * t;
                    float distance = Vector2.Distance(km, on);

                    if (distance >= best)
                        continue;

                    best = distance;
                    found = index;
                    point = on;
                }
            }

            return found < 0
                ? new Hit(float.MaxValue, 0f, 0f, km)
                : new Hit(best, segments[found].Depth, segments[found].Width, point);
        }

        static int Cell(float km) => Mathf.FloorToInt(km / CellKm);

        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;


        // A binary heap, because System.Collections.Generic.PriorityQueue is .NET 6 and this builds
        // against Unity's profile. It also keeps the order it drained in, which is exactly the
        // upstream to downstream ordering the accumulation pass needs
        sealed class Heap
        {
            readonly int[] items;
            readonly float[] keys;
            readonly List<int> drained;
            int count;

            public Heap(int capacity)
            {
                items = new int[capacity + 2];
                keys = new float[capacity + 2];
                drained = new List<int>(capacity);
            }

            public int Count => count;

            public int[] Drained => drained.ToArray();

            public void Push(int item, float key)
            {
                int at = ++count;
                items[at] = item;
                keys[at] = key;

                while (at > 1 && keys[at / 2] > keys[at])
                {
                    Swap(at, at / 2);
                    at /= 2;
                }
            }

            public int Pop()
            {
                int top = items[1];
                drained.Add(top);

                items[1] = items[count];
                keys[1] = keys[count];
                count--;

                int at = 1;

                while (true)
                {
                    int low = at, left = at * 2, right = left + 1;

                    if (left <= count && keys[left] < keys[low]) low = left;
                    if (right <= count && keys[right] < keys[low]) low = right;
                    if (low == at) break;

                    Swap(at, low);
                    at = low;
                }

                return top;
            }

            void Swap(int a, int b)
            {
                (items[a], items[b]) = (items[b], items[a]);
                (keys[a], keys[b]) = (keys[b], keys[a]);
            }
        }
    }
}
