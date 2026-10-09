using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    // The imperial trunk roads. They run between the cities and the seat of each province and
    // nowhere else, so a village is somewhere you leave the road to reach. Each one is walked over
    // the real ground with climbing priced against distance, which is why they bend around the
    // divides instead of going over them
    public sealed class WorldRoads
    {
        public readonly struct Hit
        {
            public readonly float DistanceKm;
            public readonly float GradeMetres;

            public bool Found => DistanceKm < float.MaxValue;

            public Hit(float distanceKm, float gradeMetres)
            {
                DistanceKm = distanceKm;
                GradeMetres = gradeMetres;
            }
        }

        struct Segment
        {
            public Vector2 A;
            public Vector2 B;
            public float HeightA;
            public float HeightB;
        }

        public const float PavedHalfWidthKm = 0.022f;
        public const float TrackHalfWidthKm = 0.010f;

        // A paved road is cut and graded; a track is worn by the people using it, so it takes the
        // ground more nearly as it finds it and goes more directly
        public float HalfWidthKm { get; private set; } = PavedHalfWidthKm;
        // A paved road is level across whatever it crosses, that is what grading means, and at a
        // watercourse it is what makes the crossing a causeway you can walk over rather than a ford
        // deep enough to stop you. A track is only half made up and dips into what it crosses
        public float Grading { get; private set; } = 1f;

        // A road is graded and a metre of climb is expensive to make; a track is worn and its
        // makers cared less. Neither will take a face it would have to cut steps into
        float climbKmPerMetre = 0.018f;
        float maxGrade = 0.16f;
        const float WaterKm = 400f;
        const float FordKm = 3f;
        const float CoverKm = 0.15f;
        // Sized to the segments it holds. A four kilometre bucket full of quarter kilometre
        // segments makes every terrain sample walk hundreds of them, and terrain sampling is the
        // one cost in this whole system that is paid per pixel of ground
        const float CellKm = 0.5f;

        // A routed edge, kept whole as well as chopped into segments: the segments answer "how far
        // is the road from here", the polyline answers "how do I walk it"
        sealed class Leg
        {
            public int From;
            public int To;
            public Vector2[] Path;
            public float LengthKm;
        }

        readonly List<Segment> segments = new List<Segment>();
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();

        // Every cell that a Nearest query could find something in: the cells the segments occupy and
        // the ring around them. Almost all of the world has no road within half a kilometre of it,
        // and the terrain asks this question once per road and once per track for every sample it
        // takes, so the answer "nothing here" has to cost one lookup, not nine
        readonly HashSet<long> occupied = new HashSet<long>();
        readonly List<Leg> legs = new List<Leg>();
        Vector2[] hubs = new Vector2[0];

        public int Count => segments.Count;
        public float LengthKm { get; private set; }

        public IEnumerable<(Vector2 A, Vector2 B)> EachSegment()
        {
            foreach (Segment segment in segments)
                yield return (segment.A, segment.B);
        }

        const float CapitalReachKm = 200f;

        // Worn tracks between the smaller places. Each is laid to whichever settlement was given as
        // its partner, so the set of pairs decides the shape of the network and this only walks it
        public static WorldRoads Tracks(IReadOnlyList<Vector2> places,
            IReadOnlyList<(int from, int to)> pairs, System.Func<Vector2, float> height,
            WorldRoute route, Vector2[] towns)
        {
            WorldRoads tracks = new WorldRoads
            {
                hubs = new List<Vector2>(places).ToArray(),
                HalfWidthKm = TrackHalfWidthKm,
                Grading = 0.55f,
                climbKmPerMetre = 0.008f,
                maxGrade = 0.26f
            };

            foreach ((int from, int to) in pairs)
                tracks.Lay(from, to, places[from], places[to], height, route, towns);

            return tracks;
        }

        public static WorldRoads Build(IReadOnlyList<Vector2> hubs, int capital,
            System.Func<Vector2, float> height, WorldRoute route, Vector2[] towns)
        {
            WorldRoads roads = new WorldRoads();
            roads.hubs = new List<Vector2>(hubs).ToArray();

            List<(int, int)> edges = SpanningEdges(hubs);

            // A spanning tree alone is not a road network. Left to itself it linked the capital to
            // Meregard by way of Thessanur, which is across the Wall and another country: the tree
            // only knows distance. An empire also builds outward from its capital, so every seat
            // within reach of Casrenne gets a road to it whether or not the tree wanted one
            if (capital >= 0)
            {
                for (int i = 0; i < hubs.Count; i++)
                {
                    if (i == capital || Vector2.Distance(hubs[capital], hubs[i]) > CapitalReachKm)
                        continue;

                    if (!edges.Contains((capital, i)) && !edges.Contains((i, capital)))
                        edges.Add((capital, i));
                }
            }

            foreach ((int from, int to) in edges)
                roads.Lay(from, to, hubs[from], hubs[to], height, route, towns);

            return roads;
        }

        // A minimum spanning tree over the hubs, every seat reachable, and not one road more than
        // that. Straight line distance decides the shape, the ground decides the route
        static List<(int, int)> SpanningEdges(IReadOnlyList<Vector2> hubs)
        {
            List<(int, int)> edges = new List<(int, int)>();

            if (hubs.Count < 2)
                return edges;

            List<int> joined = new List<int> { 0 };
            List<int> left = new List<int>();

            for (int i = 1; i < hubs.Count; i++)
                left.Add(i);

            while (left.Count > 0)
            {
                float best = float.MaxValue;
                int bestFrom = 0, bestTo = 0, bestAt = 0;

                for (int a = 0; a < joined.Count; a++)
                for (int b = 0; b < left.Count; b++)
                {
                    float span = Vector2.Distance(hubs[joined[a]], hubs[left[b]]);

                    if (span >= best)
                        continue;

                    best = span;
                    bestFrom = joined[a];
                    bestTo = left[b];
                    bestAt = b;
                }

                edges.Add((bestFrom, bestTo));
                joined.Add(bestTo);
                left.RemoveAt(bestAt);
            }

            return edges;
        }

        void Lay(int fromHub, int toHub, Vector2 from, Vector2 to,
            System.Func<Vector2, float> height, WorldRoute route, Vector2[] towns)
        {
            Vector2[] planned = route.Plan(from, to,
                new WorldRoute.Terms(climbKmPerMetre, maxGrade, WaterKm, FordKm, CoverKm));

            if (planned == null || planned.Length < 2)
                planned = new[] { from, to };

            // Walked back down to a kilometre a step. The router hands back long straight runs
            // wherever the country allowed one, and a segment that long is graded as a viaduct,
            // the roadbed is levelled between its ends and the ground under it is ignored
            List<Vector2> path = Resample(planned, towns);

            float length = 0f;
            for (int i = 0; i < path.Count - 1; i++)
                length += Vector2.Distance(path[i], path[i + 1]);

            legs.Add(new Leg { From = fromHub, To = toHub, Path = path.ToArray(), LengthKm = length });

            for (int i = 0; i < path.Count - 1; i++)
                Add(new Segment
                {
                    A = path[i],
                    B = path[i + 1],
                    HeightA = height(path[i]),
                    HeightB = height(path[i + 1])
                });
        }

        const float SegmentKm = 1f;

        // Closer than this to a town the road is cut into quarter kilometre lengths. The roadbed
        // height is interpolated along a segment, and a kilometre long one laid through a town
        // levelled to a pad reads its height from two points that are both outside the pad - so the
        // grading pulled the ground back up to the natural hillside inside the town it had just been
        // flattened for, and left a three-metre step across a building plot. Everywhere else a
        // kilometre is fine, and the difference is a third of the cost of sampling the world
        const float FineKm = 0.25f;
        const float TownReachKm = 1.5f;

        static List<Vector2> Resample(Vector2[] path, Vector2[] towns)
        {
            List<Vector2> coarse = new List<Vector2> { path[0] };

            for (int i = 0; i < path.Length - 1; i++)
                Chop(coarse, path[i], path[i + 1], SegmentKm);

            List<Vector2> walked = new List<Vector2> { coarse[0] };

            for (int i = 0; i < coarse.Count - 1; i++)
                Chop(walked, coarse[i], coarse[i + 1],
                    Near(coarse[i], towns) || Near(coarse[i + 1], towns) ? FineKm : SegmentKm);

            return walked;
        }

        static void Chop(List<Vector2> into, Vector2 from, Vector2 to, float step)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / step));

            for (int s = 1; s <= steps; s++)
                into.Add(Vector2.Lerp(from, to, s / (float)steps));
        }

        static bool Near(Vector2 km, Vector2[] towns)
        {
            if (towns == null)
                return false;

            foreach (Vector2 town in towns)
                if (Vector2.Distance(km, town) < TownReachKm)
                    return true;

            return false;
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

                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    occupied.Add(Key(x + dx, y + dy));
            }
        }

        public Hit Nearest(Vector2 km)
        {
            int cx = Cell(km.x), cy = Cell(km.y);

            if (!occupied.Contains(Key(cx, cy)))
                return new Hit(float.MaxValue, 0f);

            float best = float.MaxValue;
            float grade = 0f;

            for (int x = cx - 1; x <= cx + 1; x++)
            for (int y = cy - 1; y <= cy + 1; y++)
            {
                if (!grid.TryGetValue(Key(x, y), out List<int> bucket))
                    continue;

                foreach (int index in bucket)
                {
                    Segment segment = segments[index];
                    Vector2 ab = segment.B - segment.A;
                    float length = ab.sqrMagnitude;
                    float t = length < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(km - segment.A, ab) / length);
                    float distance = Vector2.Distance(km, segment.A + ab * t);

                    if (distance >= best)
                        continue;

                    best = distance;
                    grade = Mathf.Lerp(segment.HeightA, segment.HeightB, t);
                }
            }

            return new Hit(best, grade);
        }

        // Where you would step onto the network from here: the nearest point on any road, not the
        // nearest town. Requiring a hub meant a journey from Ashmere had to walk forty seven
        // kilometres to Highwall before it could use a road, so almost nothing ever did
        struct Anchor
        {
            public int Leg;
            public int Index;
            public float DistanceKm;
        }

        bool NearestOnNetwork(Vector2 km, out Anchor anchor)
        {
            anchor = new Anchor { Leg = -1, Index = 0, DistanceKm = float.MaxValue };

            for (int l = 0; l < legs.Count; l++)
            for (int i = 0; i < legs[l].Path.Length; i++)
            {
                float d = Vector2.Distance(km, legs[l].Path[i]);

                if (d >= anchor.DistanceKm)
                    continue;

                anchor = new Anchor { Leg = l, Index = i, DistanceKm = d };
            }

            return anchor.Leg >= 0;
        }

        // Measures along the leg only. Measuring from whatever was already in the list counted the
        // cross country walk onto the road as road, which made every road journey look too long to
        // be worth taking
        static float Walk(Leg leg, int from, int to, List<Vector2> into)
        {
            float length = 0f;
            int step = to >= from ? 1 : -1;

            for (int i = from; ; i += step)
            {
                if (i != from) length += Vector2.Distance(leg.Path[i - step], leg.Path[i]);
                into.Add(leg.Path[i]);

                if (i == to) break;
            }

            return length;
        }

        // The way between two points if you use the roads. Whether you should is not decided here:
        // a road is longer in kilometres and shorter in days, so only something that knows how fast
        // each is travelled can weigh them. TravelPlan does that
        // The way to the road and the way off it at the other end are journeys in their own right,
        // Ashmere is thirty six kilometres from the nearest paved road. Left as the straight lines
        // they were, they walked through marsh and lakes that the routed part of the same journey
        // had gone round, which is how a road trip ended up with nearly a kilometre of wading in it
        public List<Vector2> Route(Vector2 from, Vector2 to, out float roadKm, out float crossKm,
            System.Func<Vector2, Vector2, Vector2[]> approach = null)
        {
            roadKm = 0f;
            crossKm = Vector2.Distance(from, to);

            if (!NearestOnNetwork(from, out Anchor entry) || !NearestOnNetwork(to, out Anchor exit))
                return new List<Vector2> { from, to };

            Leg legA = legs[entry.Leg];
            Leg legB = legs[exit.Leg];
            List<Vector2> path = new List<Vector2> { from };

            if (entry.Leg == exit.Leg)
            {
                roadKm = Walk(legA, entry.Index, exit.Index, path);
                path.Add(to);
                return Approach(path, approach, entry.DistanceKm + exit.DistanceKm, out crossKm);
            }

            // Either end of the entry leg can reach either end of the exit leg. There are four ways
            // round and no reason to guess: cost all of them and keep the shortest
            float best = float.MaxValue;
            List<Vector2> bestPath = null;
            float bestRoad = 0f;

            for (int a = 0; a < 2; a++)
            for (int b = 0; b < 2; b++)
            {
                int endA = a == 0 ? 0 : legA.Path.Length - 1;
                int endB = b == 0 ? 0 : legB.Path.Length - 1;
                int hubA = a == 0 ? legA.From : legA.To;
                int hubB = b == 0 ? legB.From : legB.To;

                List<int> hops = HubPath(hubA, hubB);
                if (hops == null) continue;

                List<Vector2> candidate = new List<Vector2> { from };
                float length = Walk(legA, entry.Index, endA, candidate);

                for (int i = 0; i < hops.Count - 1; i++)
                {
                    Leg hop = LegBetween(hops[i], hops[i + 1]);
                    if (hop == null) { candidate = null; break; }

                    length += Walk(hop, hop.From == hops[i] ? 0 : hop.Path.Length - 1,
                        hop.From == hops[i] ? hop.Path.Length - 1 : 0, candidate);
                }

                if (candidate == null) continue;

                length += Walk(legB, endB, exit.Index, candidate);
                candidate.Add(to);

                if (length >= best) continue;

                best = length;
                bestPath = candidate;
                bestRoad = length;
            }

            if (bestPath == null)
                return new List<Vector2> { from, to };

            roadKm = bestRoad;
            return Approach(bestPath, approach, entry.DistanceKm + exit.DistanceKm, out crossKm);
        }

        // The first and last hops of a road journey are the ones off the network. Each is replanned
        // over the ground and spliced back in, the rest of the path is road and stays as laid
        static List<Vector2> Approach(List<Vector2> path,
            System.Func<Vector2, Vector2, Vector2[]> approach, float straightKm, out float crossKm)
        {
            crossKm = straightKm;

            if (approach == null || path.Count < 3)
                return path;

            Vector2[] onto = approach(path[0], path[1]);
            Vector2[] away = approach(path[path.Count - 2], path[path.Count - 1]);

            List<Vector2> whole = new List<Vector2>(onto);

            for (int i = 2; i < path.Count - 2; i++)
                whole.Add(path[i]);

            whole.AddRange(away);

            crossKm = WorldPaths.Length(onto) + WorldPaths.Length(away);
            return whole;
        }

        Leg LegBetween(int a, int b)
        {
            foreach (Leg leg in legs)
                if ((leg.From == a && leg.To == b) || (leg.From == b && leg.To == a)) return leg;

            return null;
        }

        List<int> HubPath(int from, int to)
        {
            if (from == to) return new List<int> { from };

            Dictionary<int, int> cameFrom = new Dictionary<int, int> { { from, -1 } };
            Queue<int> open = new Queue<int>();
            open.Enqueue(from);

            while (open.Count > 0)
            {
                int at = open.Dequeue();

                if (at == to)
                {
                    List<int> path = new List<int>();
                    for (int step = to; step != -1; step = cameFrom[step]) path.Add(step);
                    path.Reverse();
                    return path;
                }

                foreach (Leg leg in legs)
                {
                    int next = leg.From == at ? leg.To : leg.To == at ? leg.From : -1;

                    if (next < 0 || cameFrom.ContainsKey(next)) continue;

                    cameFrom[next] = at;
                    open.Enqueue(next);
                }
            }

            return null;
        }

        static int Cell(float km) => Mathf.FloorToInt(km / CellKm);

        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
