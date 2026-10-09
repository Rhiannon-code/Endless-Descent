using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    public sealed class WorldRoute
    {
        public const float CellKm = 1.5f;

        // What a route is willing to put up with. Everything is priced in kilometres of easy going,
        // so one number can be compared against another
        public readonly struct Terms
        {
            public readonly float ClimbKmPerMetre;
            public readonly float MaxGrade;
            public readonly float WaterKm;
            public readonly float FordKm;
            public readonly float CoverKm;
            public readonly System.Func<Vector2, bool> Bridged;

            public Terms(float climbKmPerMetre, float maxGrade, float waterKm, float fordKm,
                float coverKm, System.Func<Vector2, bool> bridged = null)
            {
                ClimbKmPerMetre = climbKmPerMetre;
                MaxGrade = maxGrade;
                WaterKm = waterKm;
                FordKm = fordKm;
                CoverKm = coverKm;
                Bridged = bridged;
            }
        }

        const byte Standing = 1;
        const byte Watercourse = 2;
        const float ImpassableKm = 4000f;
        public static float Lean = 1.6f;

        readonly Rect area;
        readonly int nx, ny;
        readonly float[] height;
        readonly byte[] flags;
        readonly byte[] cover;
        readonly int[] landmass;
        int landmasses;

        readonly float[] best;
        readonly int[] cameFrom;
        readonly int[] seen;
        int search;

        public int Cells => nx * ny;

        public WorldRoute(Rect area, System.Func<Vector2, SurfaceSample> sample,
            System.Func<Vector2, bool> standing)
        {
            this.area = area;
            nx = Mathf.Max(2, Mathf.CeilToInt(area.width / CellKm));
            ny = Mathf.Max(2, Mathf.CeilToInt(area.height / CellKm));

            height = new float[nx * ny];
            flags = new byte[nx * ny];
            cover = new byte[nx * ny];
            best = new float[nx * ny];
            cameFrom = new int[nx * ny];
            seen = new int[nx * ny];

            System.Threading.Tasks.Parallel.For(0, ny, y =>
            {
                for (int x = 0; x < nx; x++)
                {
                    int i = y * nx + x;
                    Vector2 km = Centre(x, y);
                    SurfaceSample here = sample(km);

                    height[i] = here.HeightMetres;
                    cover[i] = (byte)Mathf.RoundToInt(WorldVegetation.Cover(here.Biome) * 255f);

                    // Asked across the cell, not only at its middle, a lake whose edge falls
                    // between two centres is water a road has to be told about, and it is cheap to
                    // ask now that dry ground answers from a dictionary probe
                    if (AnyStanding(km, standing)) flags[i] |= Standing;
                    if (here.RiverKm < CellKm * 0.5f) flags[i] |= Watercourse;
                }
            });

            Dilate();
            landmass = new int[nx * ny];
            Separate();
        }

        public int Landmasses => landmasses;

        // Flood fill the dry cells. Everything that can be walked to without getting wet shares an
        // answer, which is the whole question "can I get there on foot" reduced to comparing two
        // integers
        void Separate()
        {
            for (int i = 0; i < landmass.Length; i++)
                landmass[i] = (flags[i] & Standing) != 0 ? -1 : -2;

            Queue<int> open = new Queue<int>();

            for (int start = 0; start < landmass.Length; start++)
            {
                if (landmass[start] != -2)
                    continue;

                int id = landmasses++;
                landmass[start] = id;
                open.Enqueue(start);

                while (open.Count > 0)
                {
                    int at = open.Dequeue();
                    int ax = at % nx, ay = at / nx;

                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int bx = ax + dx, by = ay + dy;

                        if (bx < 0 || by < 0 || bx >= nx || by >= ny) continue;

                        int next = by * nx + bx;

                        if (landmass[next] != -2) continue;

                        landmass[next] = id;
                        open.Enqueue(next);
                    }
                }
            }
        }

        // Which body of land this stands on. Water answers with the nearest land rather than -1, a
        // port sits on a shore cell that the water dilation may have claimed, and a harbour is not
        // a different country from the town behind it
        public int Landmass(Vector2 km)
        {
            int at = Index(km);

            if (landmass[at] >= 0)
                return landmass[at];

            int ax = at % nx, ay = at / nx;

            for (int reach = 1; reach <= 4; reach++)
            for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                int bx = ax + dx, by = ay + dy;

                if (bx < 0 || by < 0 || bx >= nx || by >= ny) continue;

                int found = landmass[by * nx + bx];

                if (found >= 0) return found;
            }

            return -1;
        }

        public bool Walkable(Vector2 from, Vector2 to)
        {
            int a = Landmass(from), b = Landmass(to);
            return a >= 0 && a == b;
        }

        // The point on a given body of land closest to somewhere else, where a ship puts in when
        // the place it is making for has no harbour of its own
        public Vector2 NearestLanding(int wanted, Vector2 from)
        {
            float best = float.MaxValue;
            Vector2 landing = from;

            for (int i = 0; i < landmass.Length; i++)
            {
                if (landmass[i] != wanted)
                    continue;

                Vector2 at = Centre(i % nx, i / nx);
                float span = Vector2.Distance(at, from);

                if (span >= best)
                    continue;

                best = span;
                landing = at;
            }

            return landing;
        }

        // Water spreads one cell inland. The field is coarser than the lakes it records, so a route
        // that hugged a flagged cell's dry neighbour still had its feet in the shallows a hundred
        // metres later. A road keeping a cell clear of the shore is also simply what roads do
        void Dilate()
        {
            byte[] edged = (byte[])flags.Clone();

            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
            {
                if ((flags[y * nx + x] & Standing) == 0)
                    continue;

                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int bx = x + dx, by = y + dy;

                    if (bx >= 0 && by >= 0 && bx < nx && by < ny)
                        edged[by * nx + bx] |= Standing;
                }
            }

            System.Array.Copy(edged, flags, flags.Length);
        }

        static bool AnyStanding(Vector2 centre, System.Func<Vector2, bool> standing)
        {
            if (standing(centre))
                return true;

            float quarter = CellKm * 0.5f;

            for (int i = 0; i < 4; i++)
                if (standing(centre + new Vector2(i < 2 ? -quarter : quarter,
                        i % 2 == 0 ? -quarter : quarter)))
                    return true;

            return false;
        }

        public Vector2 Centre(int x, int y) =>
            new Vector2(area.xMin + (x + 0.5f) * CellKm, area.yMin + (y + 0.5f) * CellKm);

        int Index(Vector2 km)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((km.x - area.xMin) / CellKm), 0, nx - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((km.y - area.yMin) / CellKm), 0, ny - 1);
            return y * nx + x;
        }

        public bool StandingWater(Vector2 km) => (flags[Index(km)] & Standing) != 0;

        // The steepest ground within a cell of here, as a gradient. A slope cap read off the coarse
        // field alone would let a route step onto a face it cannot climb the moment it leaves it
        public float Grade(Vector2 from, Vector2 to)
        {
            float run = Vector2.Distance(from, to) * WorldSurface.MetresPerKm;
            return run < 1f ? 0f : Mathf.Abs(height[Index(to)] - height[Index(from)]) / run;
        }

        // A* over the field. Returns the way in kilometres, from exactly where you stood to exactly
        // where you asked for, or null if the two are not joined at all
        // Main thread only, the search scribbles on scratch space the field owns, so two journeys
        // planned at once would read each other's working
        public Vector2[] Plan(Vector2 fromKm, Vector2 toKm, Terms terms)
        {
            int start = Index(fromKm), goal = Index(toKm);

            if (start == goal)
                return new[] { fromKm, toKm };

            if (landmass != null && !Walkable(fromKm, toKm))
                return null;

            // Stamped rather than cleared, a cell whose stamp is not this search has not been
            // reached yet, whatever is left in it from the last journey
            search++;

            Vector2 target = Centre(goal % nx, goal / nx);
            MinHeap open = new MinHeap(nx * ny);

            best[start] = 0f;
            cameFrom[start] = -1;
            seen[start] = search;
            open.Push(start, Vector2.Distance(Centre(start % nx, start / nx), target) * Lean);

            while (open.Count > 0)
            {
                int at = open.Pop();

                if (at == goal)
                    return Unwind(cameFrom, start, goal, fromKm, toKm, terms);

                int ax = at % nx, ay = at / nx;
                Vector2 here = Centre(ax, ay);

                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int bx = ax + dx, by = ay + dy;
                    if (bx < 0 || by < 0 || bx >= nx || by >= ny) continue;

                    int next = by * nx + bx;
                    Vector2 there = Centre(bx, by);

                    float step = Vector2.Distance(here, there);
                    float climb = Mathf.Abs(height[next] - height[at]);
                    float grade = climb / (step * WorldSurface.MetresPerKm);

                    float cost = step * (1f + cover[next] / 255f * terms.CoverKm)
                                 + climb * terms.ClimbKmPerMetre;

                    // Too steep to walk is not a price, it is a refusal, but it is charged as a
                    // vast price rather than a closed door, so a place ringed by mountains is still
                    // reachable by the least bad way instead of being cut off the map
                    if (grade > terms.MaxGrade)
                        cost += ImpassableKm;

                    if ((flags[next] & Standing) != 0)
                        cost += terms.WaterKm;

                    // A watercourse is paid for once, where it is crossed, and not at all where
                    // somebody has already built the crossing
                    if ((flags[next] & Watercourse) != 0 && (flags[at] & Watercourse) == 0
                        && (terms.Bridged == null || !terms.Bridged(there)))
                        cost += terms.FordKm;

                    float through = best[at] + cost;

                    if (seen[next] == search && through >= best[next])
                        continue;

                    seen[next] = search;
                    best[next] = through;
                    cameFrom[next] = at;
                    open.Push(next, through + Vector2.Distance(there, target) * Lean);
                }
            }

            return null;
        }

        Vector2[] Unwind(int[] cameFrom, int start, int goal, Vector2 fromKm, Vector2 toKm, Terms terms)
        {
            List<Vector2> cells = new List<Vector2>();

            for (int at = goal; at >= 0 && at != start; at = cameFrom[at])
                cells.Add(Centre(at % nx, at / nx));

            cells.Add(fromKm);
            cells.Reverse();
            cells.Add(toKm);

            return Straighten(cells, terms);
        }

        // A grid path turns in eighths of a circle and nothing on the ground does. Corners are cut
        // wherever the shortcut crosses no worse ground than the dog leg it replaces, which leaves
        // the detours that were forced by the country and removes the ones forced by the grid
        Vector2[] Straighten(List<Vector2> path, Terms terms)
        {
            List<Vector2> pulled = new List<Vector2> { path[0] };
            int at = 0;

            while (at < path.Count - 1)
            {
                int furthest = at + 1;

                for (int reach = at + 2; reach < path.Count && reach <= at + 8; reach++)
                    if (Clear(path[at], path[reach], terms))
                        furthest = reach;

                pulled.Add(path[furthest]);
                at = furthest;
            }

            return pulled.ToArray();
        }

        // The shortcut has to be walkable, not merely dry. Testing only for water let the road from
        // Casrenne to Ilsemer cut the corner it had carefully routed round, the search found the
        // pass and the straightening put the road back over the fourteen-hundred-metre crest beside
        // it, because nothing in here was looking at how steep the line it drew was
        bool Clear(Vector2 from, Vector2 to, Terms terms)
        {
            float span = Vector2.Distance(from, to);
            int steps = Mathf.CeilToInt(span / (CellKm * 0.5f));
            float run = span / steps * WorldSurface.MetresPerKm;
            float last = height[Index(from)];

            for (int i = 0; i <= steps; i++)
            {
                int cell = Index(Vector2.Lerp(from, to, i / (float)steps));

                if ((flags[cell] & Standing) != 0)
                    return false;

                if (i > 0 && Mathf.Abs(height[cell] - last) / run > terms.MaxGrade)
                    return false;

                last = height[cell];
            }

            return true;
        }

        // Smallest first, keyed by the estimate. A cell can be pushed more than once, the stale copy
        // is popped later with a worse key and is harmless, which is cheaper than finding and
        // rewriting the entry it supersedes
        sealed class MinHeap
        {
            int[] items;
            float[] keys;
            int count;

            public MinHeap(int capacity)
            {
                items = new int[Mathf.Max(16, capacity / 4)];
                keys = new float[items.Length];
            }

            public int Count => count;

            public void Push(int item, float key)
            {
                if (++count >= items.Length)
                {
                    System.Array.Resize(ref items, items.Length * 2);
                    System.Array.Resize(ref keys, keys.Length * 2);
                }

                int at = count;
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
