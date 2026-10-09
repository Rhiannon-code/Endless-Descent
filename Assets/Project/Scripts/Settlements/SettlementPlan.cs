using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Settlements
{
    public enum StreetKind { Artery, Lane, Alley, Civic }

    public struct Street
    {
        public Vector2 A;
        public Vector2 B;
        public float Width;
        public StreetKind Kind;

        public Vector2 Centre => (A + B) * 0.5f;
        public float Length => Vector2.Distance(A, B);
        public float Yaw => Mathf.Atan2(B.y - A.y, B.x - A.x) * Mathf.Rad2Deg;
    }

    public struct Building
    {
        public Vector2 Centre;
        public Vector2 Size;
        public float Yaw;
        public int Storeys;
        public float Ground;
        public bool Civic;
        public RoomPurpose Purpose;
    }

    public sealed class SettlementPlan
    {
        public Rect Bounds;
        public Rect Water;
        public Vector2 Square;
        public float SquareRadius;
        public Rect Civic;
        public bool HasCivic;
        // Where the ground is, in the settlement's own local metres. A settlement built on its own
        // supplies a private noise field, one standing in the world is handed WorldSurface instead,
        // which is what puts a town on the same ground the player walked in over
        public System.Func<float, float, float> Ground;

        public float Height(float x, float z) => Ground(x, z);
        public float Height(Vector2 at) => Ground(at.x, at.y);
        public readonly List<Street> Streets = new List<Street>();
        public readonly List<Building> Buildings = new List<Building>();
        public readonly List<Vector2> Gates = new List<Vector2>();

        // You arrive at a gate looking in, the way a traveller would, not standing in the market
        public Vector2 ArrivalPoint => Gates.Count > 0 ? Gates[0] : Square;

        public Vector3 Arrival =>
            new Vector3(ArrivalPoint.x, Height(ArrivalPoint) + 1.2f, ArrivalPoint.y);

        public Quaternion ArrivalFacing
        {
            get
            {
                Vector2 inward = Square - ArrivalPoint;
                return inward.sqrMagnitude < 0.01f
                    ? Quaternion.identity
                    : Quaternion.LookRotation(new Vector3(inward.x, 0f, inward.y).normalized, Vector3.up);
            }
        }
    }

    // Medieval towns were not laid out, they accumulated: roads left the market for somewhere else,
    // rings grew where the walls used to be, and buildings crowded whatever frontage existed. So
    // streets are grown first and buildings are packed ALONG them, rather than space being carved
    // into rectangles
    // The one deliberately regular part is the civic quarter, which is reserved before growth starts
    // so the organic streets never get into it
    public static class SettlementLayout
    {
        // An oriented box, because a building that fronts a crooked lane is not axis aligned and
        // an axis aligned test would either overlap or leave gaps everywhere
        readonly struct Box
        {
            public readonly Vector2 Centre;
            public readonly Vector2 Half;
            public readonly float Yaw;
            public readonly bool IsStreet;

            public Box(Vector2 centre, Vector2 size, float yaw, bool street)
            {
                Centre = centre;
                Half = size * 0.5f;
                Yaw = yaw;
                IsStreet = street;
            }

            public float Radius => Half.magnitude;

            public void Corners(Vector2[] into)
            {
                float c = Mathf.Cos(Yaw);
                float s = Mathf.Sin(Yaw);

                into[0] = Centre + new Vector2(-Half.x * c + Half.y * s, -Half.x * s - Half.y * c);
                into[1] = Centre + new Vector2(Half.x * c + Half.y * s, Half.x * s - Half.y * c);
                into[2] = Centre + new Vector2(Half.x * c - Half.y * s, Half.x * s + Half.y * c);
                into[3] = Centre + new Vector2(-Half.x * c - Half.y * s, -Half.x * s + Half.y * c);
            }
        }

        sealed class Space
        {
            readonly Dictionary<Vector2Int, List<Box>> bins = new Dictionary<Vector2Int, List<Box>>();
            readonly Vector2[] mine = new Vector2[4];
            readonly Vector2[] theirs = new Vector2[4];

            const float Cell = 20f;

            static Vector2Int Key(float x, float y) =>
                new Vector2Int(Mathf.FloorToInt(x / Cell), Mathf.FloorToInt(y / Cell));

            public void Add(Box box)
            {
                float r = box.Radius;
                Vector2Int min = Key(box.Centre.x - r, box.Centre.y - r);
                Vector2Int max = Key(box.Centre.x + r, box.Centre.y + r);

                for (int x = min.x; x <= max.x; x++)
                {
                    for (int y = min.y; y <= max.y; y++)
                    {
                        Vector2Int key = new Vector2Int(x, y);

                        if (!bins.TryGetValue(key, out List<Box> list))
                            bins[key] = list = new List<Box>();

                        list.Add(box);
                    }
                }
            }

            public bool Blocked(Box box, float margin, out bool byStreet)
            {
                byStreet = false;

                float r = box.Radius;
                Vector2Int min = Key(box.Centre.x - r, box.Centre.y - r);
                Vector2Int max = Key(box.Centre.x + r, box.Centre.y + r);

                for (int x = min.x; x <= max.x; x++)
                {
                    for (int y = min.y; y <= max.y; y++)
                    {
                        if (!bins.TryGetValue(new Vector2Int(x, y), out List<Box> list))
                            continue;

                        foreach (Box other in list)
                        {
                            if (!Overlaps(box, other, margin))
                                continue;

                            byStreet = other.IsStreet;
                            return true;
                        }
                    }
                }

                return false;
            }

            bool Overlaps(Box a, Box b, float margin)
            {
                if ((a.Centre - b.Centre).sqrMagnitude > Mathf.Pow(a.Radius + b.Radius + margin, 2f))
                    return false;

                a.Corners(mine);
                b.Corners(theirs);

                return !Separated(a.Yaw, margin) && !Separated(a.Yaw + Mathf.PI * 0.5f, margin) &&
                       !Separated(b.Yaw, margin) && !Separated(b.Yaw + Mathf.PI * 0.5f, margin);
            }

            bool Separated(float angle, float margin)
            {
                Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                Span(mine, axis, out float aMin, out float aMax);
                Span(theirs, axis, out float bMin, out float bMax);

                return aMax < bMin - margin || bMax < aMin - margin;
            }

            static void Span(Vector2[] corners, Vector2 axis, out float min, out float max)
            {
                min = max = Vector2.Dot(corners[0], axis);

                for (int i = 1; i < 4; i++)
                {
                    float d = Vector2.Dot(corners[i], axis);
                    min = Mathf.Min(min, d);
                    max = Mathf.Max(max, d);
                }
            }
        }

        public static SettlementPlan Generate(SettlementGrowth definition, int seed,
            System.Func<float, float, float> ground = null)
        {
            DeterministicRandom rng = new DeterministicRandom(seed);

            TerrainField field = new TerrainField(seed, definition.Relief, definition.TerrainScale);
            SettlementPlan plan = new SettlementPlan { Ground = ground ?? field.Height };

            Vector2 extent = definition.Extent;
            Vector2 half = extent * 0.5f;
            plan.Bounds = new Rect(-half.x, -half.y, extent.x, extent.y);

            if (definition.Coastal)
            {
                float water = Mathf.Min(40f, extent.y * 0.2f);
                plan.Water = new Rect(-half.x, half.y - water, extent.x, water);
                half.y -= water;
            }

            ReserveCivic(definition, ref rng, plan, half);
            GrowStreets(definition, ref rng, plan, half);
            LayCivicGrid(definition, plan);
            PackBuildings(definition, ref rng, plan, half);
            PlaceGates(plan, half);

            return plan;
        }

        // One gateway per side, a little inside the line the rampart runs along, so arriving puts
        // you at the edge of the place looking into it
        static void PlaceGates(SettlementPlan plan, Vector2 half)
        {
            Rect bounds = plan.Bounds;

            // Outside the line the rampart runs along, not inside it. Arriving should put the wall
            // and its gateway between you and the town, so the first thing you do is walk in
            float outside = 16f;

            plan.Gates.Add(new Vector2(bounds.center.x, bounds.yMin - outside));
            plan.Gates.Add(new Vector2(bounds.center.x, bounds.yMax + outside));
            plan.Gates.Add(new Vector2(bounds.xMin - outside, bounds.center.y));
            plan.Gates.Add(new Vector2(bounds.xMax + outside, bounds.center.y));

            // Nearest to the market first, so the walk in is the short one
            plan.Gates.Sort((a, b) =>
                (a - plan.Square).sqrMagnitude.CompareTo((b - plan.Square).sqrMagnitude));
        }

        static void ReserveCivic(SettlementGrowth definition, ref DeterministicRandom rng,
            SettlementPlan plan, Vector2 half)
        {
            float width = definition.CivicQuarter;
            plan.SquareRadius = definition.SquareRadius;

            if (width <= 0f)
            {
                plan.HasCivic = false;
                plan.Square = Vector2.zero;
                return;
            }

            Vector2 at = new Vector2(rng.Range(-width * 0.15f, width * 0.15f),
                rng.Range(-width * 0.15f, width * 0.15f));

            plan.Civic = new Rect(at.x - width * 0.5f, at.y - width * 0.5f, width, width);
            plan.HasCivic = true;

            // The market sits beside the civic quarter, not on it: a palace and a fish stall do not
            // share a plot
            float angle = rng.Range(0f, Mathf.PI * 2f);
            float distance = width * 0.5f + plan.SquareRadius + 14f;
            plan.Square = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            plan.Square = new Vector2(
                Mathf.Clamp(plan.Square.x, -half.x * 0.6f, half.x * 0.6f),
                Mathf.Clamp(plan.Square.y, -half.y * 0.6f, half.y * 0.6f));
        }

        static void GrowStreets(SettlementGrowth definition, ref DeterministicRandom rng,
            SettlementPlan plan, Vector2 half)
        {
            float wander = definition.Wander;

            // Arteries, the roads that leave for somewhere else, wandering as they go
            for (int i = 0; i < definition.Arteries; i++)
            {
                float angle = i * (Mathf.PI * 2f / definition.Arteries) + rng.Range(-0.3f, 0.3f);
                Vector2 point = plan.Square + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * plan.SquareRadius;
                float heading = angle;

                for (int step = 0; step < definition.ArteryLength; step++)
                {
                    heading += rng.Range(-wander, wander);
                    float run = rng.Range(definition.StepRange.x, definition.StepRange.y);
                    Vector2 next = point + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * run;

                    if (!Inside(next, half, 0.94f) || InCivic(plan, next, 4f))
                        break;

                    Add(plan, point, next, definition.MainStreetWidth, StreetKind.Artery);
                    point = next;
                }
            }

            // Rings, where a wall used to stand, so they are round but never circular
            for (int ring = 0; ring < definition.Rings; ring++)
            {
                float radius = plan.SquareRadius + (ring + 1) * definition.RingGap;
                int steps = rng.NextInt(10, 17);
                Vector2[] points = new Vector2[steps];

                for (int k = 0; k < steps; k++)
                {
                    float angle = k * (Mathf.PI * 2f / steps);
                    points[k] = plan.Square + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                        (radius * rng.Range(0.94f, 1.06f));
                }

                for (int k = 0; k < steps; k++)
                {
                    Vector2 a = points[k];
                    Vector2 b = points[(k + 1) % steps];

                    if (Inside(a, half, 0.94f) && Inside(b, half, 0.94f))
                        Add(plan, a, b, definition.LaneWidth, StreetKind.Lane);
                }
            }

            // Lanes, short crooked branches off whatever exists. Dead ends are wanted, not avoided
            int existing = plan.Streets.Count;
            if (existing == 0)
                return;

            for (int i = 0; i < definition.Lanes; i++)
            {
                Street parent = plan.Streets[rng.NextInt(existing)];
                Vector2 point = Vector2.Lerp(parent.A, parent.B, rng.NextFloat());
                float heading = Mathf.Atan2(parent.B.y - parent.A.y, parent.B.x - parent.A.x) +
                                (rng.Chance(0.5f) ? Mathf.PI * 0.5f : -Mathf.PI * 0.5f);

                int length = rng.NextInt(1, 4);

                for (int step = 0; step < length; step++)
                {
                    heading += rng.Range(-wander * 1.7f, wander * 1.7f);
                    float run = rng.Range(definition.StepRange.x, definition.StepRange.y) * 0.65f;
                    Vector2 next = point + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * run;

                    if (!Inside(next, half, 0.94f) || InCivic(plan, next, 4f))
                        break;

                    Add(plan, point, next, definition.AlleyWidth, StreetKind.Alley);
                    point = next;
                }
            }
        }

        static void LayCivicGrid(SettlementGrowth definition, SettlementPlan plan)
        {
            if (!plan.HasCivic)
                return;

            Rect quarter = plan.Civic;
            int lines = Mathf.Max(2, Mathf.RoundToInt(quarter.width / definition.CivicSpacing));
            float step = quarter.width / lines;

            for (int k = 0; k <= lines; k++)
            {
                float offset = k * step;

                plan.Streets.Add(new Street
                {
                    A = new Vector2(quarter.xMin + offset, quarter.yMin),
                    B = new Vector2(quarter.xMin + offset, quarter.yMax),
                    Width = definition.LaneWidth,
                    Kind = StreetKind.Civic
                });

                plan.Streets.Add(new Street
                {
                    A = new Vector2(quarter.xMin, quarter.yMin + offset),
                    B = new Vector2(quarter.xMax, quarter.yMin + offset),
                    Width = definition.LaneWidth,
                    Kind = StreetKind.Civic
                });
            }
        }

        static void PackBuildings(SettlementGrowth definition, ref DeterministicRandom rng,
            SettlementPlan plan, Vector2 half)
        {
            Space space = new Space();

            foreach (Street street in plan.Streets)
            {
                float length = street.Length;
                if (length < 0.01f)
                    continue;

                space.Add(new Box(street.Centre, new Vector2(length, street.Width),
                    street.Yaw * Mathf.Deg2Rad, true));
            }

            space.Add(new Box(plan.Square, Vector2.one * (plan.SquareRadius * 2f), 0f, true));

            List<RoomPurpose> trades = Deal(definition.Trades, ref rng);
            List<RoomPurpose> civic = Deal(definition.Civic, ref rng);
            int nextTrade = 0;
            int nextCivic = 0;

            // Prime frontage first, a smith wants the high street, not the back of an alley
            List<Street> order = new List<Street>(plan.Streets);
            order.Sort((a, b) => Rank(a.Kind).CompareTo(Rank(b.Kind)));

            float reach = Mathf.Max(half.x, half.y);

            foreach (Street street in order)
            {
                float length = street.Length;
                if (length < 8f)
                    continue;

                float angle = street.Yaw * Mathf.Deg2Rad;
                Vector2 along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 out2 = new Vector2(-along.y, along.x);
                bool isCivic = street.Kind == StreetKind.Civic;

                float fromSquare = Vector2.Distance(street.Centre, plan.Square) / reach;

                for (int side = -1; side <= 1; side += 2)
                {
                    float t = rng.Range(1.5f, 5f);

                    while (t < length - 3f)
                    {
                        Vector2 size = new Vector2(
                            rng.Range(definition.BuildingWidth.x, definition.BuildingWidth.y),
                            rng.Range(definition.BuildingDepth.x, definition.BuildingDepth.y));

                        if (isCivic)
                            size *= 1.5f;

                        Vector2 at = street.A + along * t +
                                     out2 * (side * (street.Width * 0.5f + size.y * 0.5f + definition.Setback));

                        if (!Inside(at, half, 0.92f))
                        {
                            t += size.x * 0.5f;
                            continue;
                        }

                        float yaw = angle + (isCivic ? 0f : rng.Range(-definition.Skew, definition.Skew));
                        Box box = new Box(at, size, yaw, false);

                        // Stored ready for Unity, front already turned to face the street, plan
                        // space measures Z up the page, Unity measures it into the screen
                        float facing = -yaw * Mathf.Rad2Deg + (side > 0 ? 180f : 0f);

                        if (space.Blocked(box, 0.3f, out bool _))
                        {
                            t += definition.Retry;
                            continue;
                        }

                        space.Add(box);

                        Vector2Int storeys = isCivic ? definition.CivicStoreys
                            : fromSquare < 0.42f ? definition.CoreStoreys : definition.EdgeStoreys;

                        RoomPurpose purpose;

                        if (isCivic && civic.Count > 0)
                            purpose = civic[nextCivic++ % civic.Count];
                        else if (trades.Count > 0)
                            purpose = trades[nextTrade++ % trades.Count];
                        else
                            purpose = RoomPurpose.Townhouse;

                        plan.Buildings.Add(new Building
                        {
                            Centre = at,
                            Size = size,
                            Yaw = facing,
                            Storeys = rng.NextInt(storeys.x, storeys.y + 1),
                            Ground = plan.Height(at),
                            Civic = isCivic,
                            Purpose = purpose
                        });

                        t += size.x + rng.Range(definition.Gap.x, definition.Gap.y);
                    }
                }
            }
        }

        static int Rank(StreetKind kind)
        {
            switch (kind)
            {
                case StreetKind.Artery: return 0;
                case StreetKind.Civic: return 1;
                case StreetKind.Lane: return 2;
                default: return 3;
            }
        }

        // A run laid along one that already exists adds tangle rather than frontage
        static void Add(SettlementPlan plan, Vector2 a, Vector2 b, float width, StreetKind kind)
        {
            Vector2 centre = (a + b) * 0.5f;
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x);

            foreach (Street other in plan.Streets)
            {
                if ((centre - other.Centre).sqrMagnitude > 144f)
                    continue;

                float difference = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, other.Yaw)) % 180f;

                if (difference < 20f || difference > 160f)
                    return;
            }

            plan.Streets.Add(new Street { A = a, B = b, Width = width, Kind = kind });
        }

        static bool Inside(Vector2 point, Vector2 half, float pad) =>
            Mathf.Abs(point.x) < half.x * pad && Mathf.Abs(point.y) < half.y * pad;

        static bool InCivic(SettlementPlan plan, Vector2 point, float pad)
        {
            if (!plan.HasCivic)
                return false;

            Rect quarter = plan.Civic;
            return point.x > quarter.xMin - pad && point.x < quarter.xMax + pad &&
                   point.y > quarter.yMin - pad && point.y < quarter.yMax + pad;
        }

        // Every listed trade appears once before any appears twice, so a town is not eight bakeries
        static List<RoomPurpose> Deal(RoomPurpose[] wanted, ref DeterministicRandom rng)
        {
            List<RoomPurpose> deck = new List<RoomPurpose>();

            if (wanted == null || wanted.Length == 0)
                return deck;

            for (int round = 0; round < 4; round++)
            {
                List<RoomPurpose> shuffled = new List<RoomPurpose>(wanted);
                rng.Shuffle(shuffled);
                deck.AddRange(shuffled);
                deck.Add(RoomPurpose.Townhouse);
                deck.Add(RoomPurpose.Townhouse);
            }

            return deck;
        }
    }
}
