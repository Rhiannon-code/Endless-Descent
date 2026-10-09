using System.Collections.Generic;
using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Data
{
    public readonly struct SurfaceSample
    {
        public readonly float HeightMetres;
        public readonly BiomeId Biome;
        public readonly float RiverKm;
        public readonly float RoadKm;

        public bool Submerged => HeightMetres < 0f;

        public SurfaceSample(float heightMetres, BiomeId biome, float riverKm, float roadKm)
        {
            HeightMetres = heightMetres;
            Biome = biome;
            RiverKm = riverKm;
            RoadKm = roadKm;
        }
    }

    // The ground between the places. Positions go in as kilometres in RegionMap's frame, heights
    // come out as metres above sea level. Deterministic, so the same coordinate answers the same
    // way in the editor, in a build and in the save file
    public sealed class WorldSurface
    {
        public const float MetresPerKm = 1000f;

        const float MountainBiomeMetres = 250f;
        const float ShoreBlendKm = 6f;
        const float LandFloorMetres = 1f;
        const float EscarpmentBlendKm = 0.22f;
        const float MaxCarveShare = 0.6f;

        // How much of the cut channel stays dry above the water, and how deep a cut has to be before
        // it is a watercourse rather than a dip in the ground
        const float Freeboard = 0.35f;
        const float MinChannelMetres = 0.35f;
        const float ValleyKm = 3f;
        // The pad has to cover the whole town, not its middle. At a 70 m flat the harness measures
        // a settlement 280 m across standing half on its pad and half on the feather, and the
        // feather is the steepest part of it
        const float TownFlatKm = 0.18f;
        const float TownFeatherKm = 0.62f;

        // A sample only ever touches the handful of features near it, so each one carries the box
        // outside which it cannot contribute. Without this every sample walked all six polygons
        readonly struct Box
        {
            readonly float minX, minY, maxX, maxY;

            public Box(Vector2[] points, float pad)
            {
                minX = minY = float.MaxValue;
                maxX = maxY = float.MinValue;

                foreach (Vector2 p in points)
                {
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }

                minX -= pad; minY -= pad; maxX += pad; maxY += pad;
            }

            public bool Excludes(Vector2 p) => p.x < minX || p.x > maxX || p.y < minY || p.y > maxY;
        }

        readonly struct Course
        {
            const int RunSegments = 8;
            const float ExactKm = 6f;

            readonly Vector2[] drawn;
            readonly Vector2[] points;
            readonly Vector2[] centres;
            readonly float[] radii;
            readonly float slack;

            public Course(Vector2[] drawn, Vector2[] meandered, float slack)
            {
                this.drawn = drawn;
                this.slack = slack;
                points = meandered;

                int runs = Mathf.Max(1, Mathf.CeilToInt((points.Length - 1) / (float)RunSegments));
                centres = new Vector2[runs];
                radii = new float[runs];

                for (int r = 0; r < runs; r++)
                {
                    int from = r * RunSegments;
                    int to = Mathf.Min(points.Length - 1, from + RunSegments);

                    Vector2 low = points[from], high = points[from];

                    for (int i = from; i <= to; i++)
                    {
                        low = Vector2.Min(low, points[i]);
                        high = Vector2.Max(high, points[i]);
                    }

                    centres[r] = (low + high) * 0.5f;
                    radii[r] = Vector2.Distance(low, high) * 0.5f;
                }
            }

            public float Distance(Vector2 km)
            {
                float rough = WorldGeometry.DistanceToPath(km, drawn);

                if (rough > ExactKm + slack)
                    return rough;

                float best = rough + slack;

                for (int r = 0; r < centres.Length; r++)
                {
                    if (Vector2.Distance(km, centres[r]) - radii[r] >= best)
                        continue;

                    int from = r * RunSegments;
                    int to = Mathf.Min(points.Length - 1, from + RunSegments);

                    for (int i = from; i < to; i++)
                        best = Mathf.Min(best, WorldGeometry.DistanceToSegment(km, points[i], points[i + 1]));
                }

                return best;
            }
        }

        readonly WorldGeography geography;
        readonly TerrainField[] fields;
        readonly TerrainField[] details;
        readonly TerrainField[] divides;
        readonly TerrainField[] hills;
        readonly TerrainField ridgeField;
        readonly TerrainField coastField;
        readonly TerrainField shoreField;
        readonly TerrainField boundaryField;
        readonly Course[] rivers;
        readonly Box[] regionBounds;
        readonly Box[] islandBounds;
        readonly Box[] rangeBounds;
        readonly Box[] riverBounds;
        readonly Box escarpmentBounds;
        readonly WorldStreams streams;
        readonly WorldRoute route;
        readonly WorldRoads roads;
        readonly WorldRoads tracks;
        Vector2[] townKm;
        float[] townHeight;

        public WorldGeography Geography => geography;

        public WorldSurface(WorldGeography geography, int seed)
        {
            this.geography = geography;
            ridgeField = new TerrainField(seed + 104729, 1f, 4f);
            coastField = new TerrainField(seed + 15486, 1f, geography.CoastWarpScaleKm);
            shoreField = new TerrainField(seed + 27644, 1f, geography.CoastWarpScaleKm * 0.6f);
            boundaryField = new TerrainField(seed + 39916, 1f, geography.BoundaryWarpScaleKm);

            // The drawn rivers are meandered once, here, so everything that asks where the Casren
            // runs, the carve, the biome valley, the stream tracer, the map, gets the same answer
            rivers = new Course[geography.Rivers.Length];
            for (int i = 0; i < rivers.Length; i++)
                rivers[i] = new Course(geography.Rivers[i].Path,
                    Meander(geography.Rivers[i].Path, seed + 5813 + i * 977), geography.MeanderKm);

            regionBounds = new Box[geography.Regions.Length];
            for (int i = 0; i < regionBounds.Length; i++)
                regionBounds[i] = new Box(geography.Regions[i].Polygon, geography.Regions[i].BlendKm);

            rangeBounds = new Box[geography.Ranges.Length];
            for (int i = 0; i < rangeBounds.Length; i++)
                rangeBounds[i] = new Box(geography.Ranges[i].Path, geography.Ranges[i].WidthKm);

            riverBounds = new Box[geography.Rivers.Length];
            for (int i = 0; i < riverBounds.Length; i++)
                riverBounds[i] = new Box(geography.Rivers[i].Path,
                    geography.Rivers[i].WidthKm + geography.MeanderKm);

            escarpmentBounds = new Box(geography.Escarpment, geography.EscarpmentReachKm);

            islandBounds = new Box[geography.Islands.Length];
            for (int i = 0; i < islandBounds.Length; i++)
                islandBounds[i] = new Box(geography.Islands[i].Polygon,
                    geography.Islands[i].BlendKm + geography.BoundaryWarpKm);

            int biomes = System.Enum.GetValues(typeof(BiomeId)).Length;
            fields = new TerrainField[biomes];
            details = new TerrainField[biomes];
            divides = new TerrainField[biomes];
            hills = new TerrainField[biomes];

            for (int i = 0; i < biomes; i++)
            {
                BiomeProfile profile = WorldGeography.Profile((BiomeId)i);
                fields[i] = new TerrainField(seed + i * 7919, 1f, profile.NoiseScaleKm);
                details[i] = new TerrainField(seed + 31337 + i * 6271, 1f, profile.DetailScaleKm);
                divides[i] = new TerrainField(seed + 90001 + i * 4813, 1f, profile.DivideScaleKm);
                hills[i] = new TerrainField(seed + 51413 + i * 3271, 1f, profile.HillScaleKm);
            }

            // Last, and against Bare rather than Sample, the network is carved into the land, so the
            // land it is carved into has to exist first, noise fields and all
            streams = WorldStreams.Trace(geography, geography.Extent(), Bare, NearestRiverKm, seed);

            // Every settlement in the survey, wherever it stands. The pads below are owed to all of
            // them, Hvarn is as much a town for standing on the far shore, and which of them the
            // roads and tracks join up is a separate question, asked once the router exists
            List<Vector2> places = new List<Vector2>();
            List<OrdovanPlaces.Place> settlements = new List<OrdovanPlaces.Place>();

            foreach (OrdovanPlaces.Place p in OrdovanPlaces.All)
            {
                if (!p.IsSettlement) continue;

                settlements.Add(p);
                places.Add(p.Km);
            }

            // Settlement pads, before anything is routed over them rather than after. A roadbed is
            // recorded at the height of the ground it was laid on, so grading a track through a town
            // whose pad was levelled afterwards left a step between the lane and the plot beside it.
            // Both fields are assigned together and last. Setting the positions first makes Level
            // dereference a height array that does not exist yet, from inside the very call that is
            // measuring the heights
            Vector2[] centres = places.ToArray();
            float[] levels = new float[centres.Length];

            for (int i = 0; i < centres.Length; i++)
                levels[i] = Unpaved(centres[i]);

            townKm = centres;
            townHeight = levels;

            // The ground everything is routed over, sampled once. Roads, tracks and every journey
            // the player ever plans read this same field, so a way that is walkable for one is
            // walkable for all of them
            route = new WorldRoute(geography.Extent(), Unroaded, Standing);

            // Skarrvald is across the Northern Sea and no road reaches it, its hub drops out here
            // rather than being special cased, because it is not on the same body of land. That used
            // to be a latitude test, which stopped being the same question the moment the far shore
            // became a shape and the Cold Reach became an island in front of it
            int mainland = route.Landmass(OrdovanPlaces.KmOf("Casrenne"));

            List<Vector2> hubs = new List<Vector2>();
            int capital = -1;

            foreach (OrdovanPlaces.Place hub in OrdovanPlaces.Hubs)
            {
                if (route.Landmass(hub.Km) != mainland)
                    continue;

                if (hub.Province == Province.Vareth)
                    capital = hubs.Count;

                hubs.Add(hub.Km);
            }

            roads = WorldRoads.Build(hubs, capital, Unpaved, route, townKm);

            // A track is laid between two places you could walk between. Pairing on distance alone
            // would run one from the Gullmarket to Grand Anchor, straight down the sea floor
            List<(int, int)> pairs = new List<(int, int)>();

            for (int i = 0; i < settlements.Count; i++)
            {
                int best = -1;
                float span = float.MaxValue;
                int here = route.Landmass(places[i]);

                for (int j = 0; j < settlements.Count; j++)
                {
                    if (j == i || settlements[j].Tier <= settlements[i].Tier) continue;
                    if (route.Landmass(places[j]) != here) continue;

                    float d = Vector2.Distance(places[i], places[j]);
                    if (d >= span) continue;

                    span = d;
                    best = j;
                }

                if (best >= 0)
                    pairs.Add((i, best));
            }

            tracks = WorldRoads.Tracks(places, pairs, Unpaved, route, townKm);
        }

        public WorldRoute Route => route;
        public int StreamSegments => streams != null ? streams.Count : 0;
        public WorldRoads Roads => roads;
        public WorldRoads Tracks => tracks;
        public int TrackSegments => tracks != null ? tracks.Count : 0;
        public float TrackLengthKm => tracks != null ? tracks.LengthKm : 0f;
        public int RoadSegments => roads != null ? roads.Count : 0;
        public float RoadLengthKm => roads != null ? roads.LengthKm : 0f;
        public float StreamLengthKm => streams != null ? streams.LengthKm : 0f;
        public float LakeAreaKm2 => streams != null ? streams.LakeAreaKm2 : 0f;

        // The surface of standing water here, a lake's level, sea level over the sea, or nothing
        public float WaterMetres(Vector2 km)
        {
            float reach = geography.CoastWarpKm + geography.ShoreWarpKm;

            if (km.y > geography.CoastNorthKm - reach && km.y >= CoastKm(km.x))
                return 0f;

            return streams != null ? streams.WaterLevel(km) : 0f;
        }
        public WorldStreams Streams => streams;

        // The surface of running water in the channel here, or zero where there is none
        // Deliberately not folded into WaterMetres. That one is asked once per cell over ninety
        // thousand cells while the world is built, and ADR 0040 got it down to a dictionary probe and
        // a latitude, putting a terrain compute back inside it would undo that. Only the water mesh
        // builder asks this, once per wet sample of a tile that has a river in it
        public float ChannelMetres(Vector2 km)
        {
            if (streams == null)
                return 0f;

            WorldStreams.Hit hit = streams.Nearest(km);

            if (!hit.Found || hit.DistanceKm >= hit.WidthKm)
                return 0f;

            // The same carve Compute applies, so the water sits in the channel that was actually cut
            // rather than in the one that was asked for. Banks win, a channel the land was too low to
            // cut is a channel with no water in it
            float bank = Bare(km);
            float bite = hit.DepthMetres * (1f - WorldGeometry.SmoothStep(0f, hit.WidthKm, hit.DistanceKm));
            float carve = Mathf.Min(bite, Mathf.Max(0f, (bank - LandFloorMetres) * MaxCarveShare));

            return carve <= MinChannelMetres ? 0f : bank - carve * Freeboard;
        }

        public float Height(Vector2 km) => Sample(km).HeightMetres;

        float Bare(Vector2 km) => Compute(km, false, false).HeightMetres;

        float Unpaved(Vector2 km) => Compute(km, true, false).HeightMetres;

        // The whole sample as the ground stands before any road is laid in it, which is what the
        // router has to plan over: the roads do not exist yet when it is built.
        SurfaceSample Unroaded(Vector2 km) => Compute(km, true, false);

        // Standing water, asked without the roads that are being laid around it
        // The lake lookup is a dictionary probe and the sea is a latitude, only where one of those
        // says there might be water is the full surface asked for a height. Asking it everywhere,
        // which is what "is this ground below zero" quietly did, cost four terrain computes per
        // cell of the route field, over ninety thousand cells, to discover that dry land is dry
        bool Standing(Vector2 km)
        {
            float level = WaterMetres(km);

            if (level > 0f)
                return Unpaved(km) < level;

            float reach = ShoreBlendKm + geography.CoastWarpKm + geography.ShoreWarpKm;

            return km.y >= geography.CoastNorthKm - reach && Unpaved(km) <= 0f;
        }

        public BiomeId Biome(Vector2 km) => Sample(km).Biome;

        public SurfaceSample Sample(Vector2 km) => Compute(km, true, true);

        SurfaceSample Compute(Vector2 km, bool includeStreams, bool includeRoads)
        {
            float riverKm = NearestRiverKm(km);

            float scarp = Scarp(km);

            // The same push for every region boundary, so it is looked up once rather than once per
            // polygon: a noise lookup is three octaves and this is the hot path of the whole world
            float warp = boundaryField.Height(km) * geography.BoundaryWarpKm * (1f - scarp);

            // A river cuts through a divide rather than climbing it, so the ridge term fades out
            // along every watercourse. Without this the Casren ran over the tops of its own ridges
            float valley = WorldGeometry.SmoothStep(0f, ValleyKm, riverKm);
            float total = 0f;

            // stackalloc rather than a field, Sample has to stay callable from a worker thread,
            // because a terrain tile is far too slow to build on the main one.
            System.Span<float> weights = stackalloc float[geography.Regions.Length];

            for (int i = 0; i < geography.Regions.Length; i++)
            {
                if (regionBounds[i].Excludes(km))
                {
                    weights[i] = 0f;
                    continue;
                }

                WorldGeography.Region region = geography.Regions[i];
                float blend = Mathf.Lerp(region.BlendKm, EscarpmentBlendKm, scarp);

                // Pushing the boundary in and out before it is blended is what turns a ruled
                // polygon edge into a border. Doing it here rather than to the polygon itself keeps
                // the transcribed Survey shape as the thing that was authored
                float signed = WorldGeometry.SignedDistance(km, region.Polygon) + warp;

                weights[i] = 1f - WorldGeometry.SmoothStep(-blend, blend, signed);
                total += weights[i];
            }

            float defaultWeight = Mathf.Max(0f, 1f - total);
            float sum = total + defaultWeight;

            float height = 0f;
            float relief = 0f;
            float best = defaultWeight;
            BiomeId biome = WorldGeography.Default;

            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f)
                    continue;

                float share = weights[i] / sum;
                BiomeId candidate = geography.Regions[i].Biome;
                BiomeProfile profile = WorldGeography.Profile(candidate);

                height += profile.BaseMetres * share;
                relief += (fields[(int)candidate].Height(km) * profile.ReliefMetres
                        + details[(int)candidate].Height(km) * profile.DetailMetres
                        + Hill(candidate, km, valley) * profile.HillMetres
                        + Divide(candidate, km, valley) * profile.DivideMetres) * share;

                if (weights[i] > best)
                {
                    best = weights[i];
                    biome = candidate;
                }
            }

            if (defaultWeight > 0f)
            {
                BiomeProfile profile = WorldGeography.Profile(WorldGeography.Default);
                float share = defaultWeight / sum;

                height += profile.BaseMetres * share;
                relief += (fields[(int)WorldGeography.Default].Height(km) * profile.ReliefMetres
                        + details[(int)WorldGeography.Default].Height(km) * profile.DetailMetres
                        + Hill(WorldGeography.Default, km, valley) * profile.HillMetres
                        + Divide(WorldGeography.Default, km, valley) * profile.DivideMetres) * share;
            }

            height += relief;

            float ridge = Ridge(km);
            if (ridge > 0f)
            {
                height += ridge;

                if (ridge >= MountainBiomeMetres)
                    biome = BiomeId.Mountain;
            }

            // A river cannot cut deeper than the ground it is cutting into. The Casren's full carve
            // is more than the marsh it crosses is tall, and taking it whole flattened a kilometre
            // of Kelder Wharf against the floor
            float carve = Carve(km);
            height -= Mathf.Min(carve, Mathf.Max(0f, (height - LandFloorMetres) * MaxCarveShare));
            height = Mathf.Max(LandFloorMetres, height);

            // The Northern Sea is a channel with a shore on both sides, not everything past a line,
            // Skarrvald's ports stand on the far one, and a half-plane of water drowned them
            float water = SeaWeight(km) * (1f - Island(km, warp));

            if (water > 0f)
            {
                height = Mathf.Lerp(height, SeaDepth(km), water);

                if (water > 0.5f)
                    biome = BiomeId.Sea;
            }

            if (includeStreams && streams != null)
            {
                WorldStreams.Hit hit = streams.Nearest(km);

                if (hit.Found && hit.DistanceKm < hit.WidthKm)
                {
                    float bite = hit.DepthMetres * (1f - WorldGeometry.SmoothStep(0f, hit.WidthKm, hit.DistanceKm));

                    height -= Mathf.Min(bite, Mathf.Max(0f, (height - LandFloorMetres) * MaxCarveShare));
                    height = Mathf.Max(LandFloorMetres, height);
                }

                riverKm = Mathf.Min(riverKm, hit.DistanceKm);
            }

            float roadKm = float.MaxValue;

            height = Level(km, height);

            if (includeRoads)
            {
                // A road is graded, not painted on, the ground is pulled toward the roadbed over a
                // verge either side, so it does not ride up and over every hummock it crosses. A
                // track is barely graded at all, which is most of what makes it feel like a track
                height = Grade(roads, km, height, ref roadKm);
                height = Grade(tracks, km, height, ref roadKm);
            }

            return new SurfaceSample(height, biome, riverKm, roadKm);
        }

        // Deepest mid-channel and rising to nothing at either shore
        // Flat where the town stands, easing back into the country over a couple of hundred metres
        float Level(Vector2 km, float height)
        {
            if (townKm == null)
                return height;

            for (int i = 0; i < townKm.Length; i++)
            {
                float dx = km.x - townKm[i].x, dy = km.y - townKm[i].y;

                if (dx * dx + dy * dy >= TownFeatherKm * TownFeatherKm)
                    continue;

                float t = 1f - WorldGeometry.SmoothStep(TownFlatKm, TownFeatherKm,
                    Mathf.Sqrt(dx * dx + dy * dy));

                return Mathf.Lerp(height, townHeight[i], t);
            }

            return height;
        }

        float Grade(WorldRoads way, Vector2 km, float height, ref float nearest)
        {
            if (way == null)
                return height;

            WorldRoads.Hit hit = way.Nearest(km);
            nearest = Mathf.Min(nearest, hit.DistanceKm);

            float reach = way.HalfWidthKm * 3f;

            if (!hit.Found || hit.DistanceKm >= reach)
                return height;

            float t = 1f - WorldGeometry.SmoothStep(0f, reach, hit.DistanceKm);
            return Mathf.Lerp(height, hit.GradeMetres, t * way.Grading);
        }

        // Where the mainland ends at this easting. Everything north of it is ocean unless an island
        // says otherwise
        public float CoastKm(float eastKm) =>
            geography.CoastNorthKm + coastField.Height(eastKm, 0f) * geography.CoastWarpKm
            + shoreField.Height(eastKm, 0f) * geography.ShoreWarpKm;

        // Shelving off the coast to a floor that then stays put. The old profile was a parabola
        // between two shores, so the "sea" was deepest exactly halfway and shallow everywhere else,
        // a ditch, not an ocean
        float SeaDepth(Vector2 km)
        {
            return geography.SeaFloorMetres
                   * WorldGeometry.SmoothStep(0f, geography.ShelfKm, km.y - CoastKm(km.x));
        }

        // Land standing in the ocean. It takes the same wandering boundary the regions take, so an
        // island's shore is as shapeless as the mainland's, and it simply refuses the sea rather
        // than raising ground of its own, the country on it is the upland the surface already
        // computed there
        float Island(Vector2 km, float warp)
        {
            float most = 0f;

            for (int i = 0; i < geography.Islands.Length; i++)
            {
                if (islandBounds[i].Excludes(km))
                    continue;

                WorldGeography.Island island = geography.Islands[i];
                // A fraction of the mainland's warp. Sixteen kilometres of wander is nothing on a
                // border four hundred long and most of an island sixty across
                float signed = WorldGeometry.SignedDistance(km, island.Polygon) + warp * 0.4f;

                most = Mathf.Max(most, 1f - WorldGeometry.SmoothStep(-island.BlendKm, island.BlendKm, signed));
            }

            return most;
        }

        float SeaWeight(Vector2 km)
        {
            // The coast moves by at most its two warps, so anything south of that is land without a
            // noise lookup being needed to say so, and that is most of the world
            if (km.y < geography.CoastNorthKm - ShoreBlendKm
                - geography.CoastWarpKm - geography.ShoreWarpKm)
                return 0f;

            float coast = CoastKm(km.x);
            return WorldGeometry.SmoothStep(coast - ShoreBlendKm, coast, km.y);
        }

        // A drawn river runs straight from one transcribed point to the next, which is a canal. This
        // walks the line at a fixed step and pushes each point sideways, so the Casren bends
        Vector2[] Meander(Vector2[] path, int seed)
        {
            if (path == null || path.Length < 2)
                return path;

            TerrainField wander = new TerrainField(seed, 1f, geography.MeanderScaleKm);
            List<Vector2> meandered = new List<Vector2>();
            float along = 0f;

            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 a = path[i], b = path[i + 1];
                float span = Vector2.Distance(a, b);
                if (span < 1e-4f) continue;

                Vector2 sideways = new Vector2(-(b.y - a.y), b.x - a.x) / span;

                // The ends stay where the Survey put them, a river that leaves its own mouth is not
                // a better river
                for (; along < span; along += geography.MeanderStepKm)
                {
                    Vector2 on = Vector2.Lerp(a, b, along / span);
                    float ends = Mathf.Min(1f, Mathf.Min(along, span - along) / 6f);

                    meandered.Add(on + sideways * wander.Height(on) * geography.MeanderKm * ends);
                }

                along -= span;
            }

            meandered.Add(path[path.Length - 1]);
            return meandered.ToArray();
        }

        // Near the Dolmarch Edge every region's blend band tightens, which is what turns the moor's
        // stand over the low ground into a scarp instead of a long hillside. The full narrowing has
        // to hold over a band rather than peak on the line itself, the moor's boundary runs a couple
        // of kilometres off the drawn Edge, and a narrowing that faded from the line would reach it
        // already half spent
        float Scarp(Vector2 km)
        {
            if (geography.Escarpment == null || geography.Escarpment.Length < 2)
                return 0f;

            if (escarpmentBounds.Excludes(km))
                return 0f;

            float distance = WorldGeometry.DistanceToPath(km, geography.Escarpment);
            return 1f - WorldGeometry.SmoothStep(geography.EscarpmentCoreKm, geography.EscarpmentReachKm, distance);
        }

        // Hills fade along a watercourse for the same reason divides do, and by the same trick: the
        // damping is applied before the mean is taken out, so a river sits at the floor of the band
        // rather than merely at its average. Damping a signed term toward zero put the Casren on a
        // hilltop twice
        float Hill(BiomeId biome, Vector2 km, float valley)
        {
            return (hills[(int)biome].Height(km) + 0.5f) * valley - 0.5f;
        }

        // Folded noise, so the crests fall in lines rather than in blobs: what a watershed looks
        // like from above is a ridge with a valley either side, not a field of lumps
        // The valley factor is applied before the mean is taken out, not after. Damping a signed
        // term toward zero at a river only says the river is average, and a bank sitting in a trough
        // was still ending up below the channel, damping the raw crest pins the river to the floor
        // of this term and nothing near it can be lower
        float Divide(BiomeId biome, Vector2 km, float valley)
        {
            float folded = 1f - 2f * Mathf.Abs(divides[(int)biome].Height(km));
            return folded * valley - 0.5f;
        }

        // A range is a crest standing on an apron. Spreading the whole rise over the full width
        // made the great ranges long soft hills you walked over without noticing, nothing in the
        // world reached thirty degrees. The apron carries a quarter of the height out to WidthKm as
        // foothills, and the rest of it climbs inside Crest, which is what makes a wall a wall
        const float ApronShare = 0.26f;
        const float PassFloor = 0.17f;
        const float PassKm = 8f;

        float Ridge(Vector2 km)
        {
            float highest = 0f;

            for (int i = 0; i < geography.Ranges.Length; i++)
            {
                if (rangeBounds[i].Excludes(km))
                    continue;

                WorldGeography.Range range = geography.Ranges[i];
                float distance = WorldGeometry.DistanceToPath(km, range.Path);
                if (distance >= range.WidthKm)
                    continue;

                float apron = (1f - WorldGeometry.SmoothStep(0f, range.WidthKm, distance)) * ApronShare;
                float crest = 1f - WorldGeometry.SmoothStep(0f, range.Crest, distance);

                // Held tighter than it was. A crest whose height swings by a factor of four opens
                // gaps of its own wherever the noise dips, and a wall with accidental holes in it is
                // not a wall, the ways through are the passes, and they are drawn
                float rough = 0.86f + 0.28f * (ridgeField.Height(km) + 0.5f);

                highest = Mathf.Max(highest,
                    range.PeakMetres * Mathf.Max(apron, crest) * rough * Pass(range, km));
            }

            return highest;
        }

        // How far the crest is let down here. A pass is a saddle, not a doorway, it takes the height
        // off over kilometres so the road climbing to it has somewhere to climb
        static float Pass(WorldGeography.Range range, Vector2 km)
        {
            if (range.Passes == null)
                return 1f;

            float open = 1f;

            foreach (Vector2 saddle in range.Passes)
                open = Mathf.Min(open, Mathf.Lerp(PassFloor, 1f,
                    WorldGeometry.SmoothStep(0f, PassKm, Vector2.Distance(km, saddle))));

            return open;
        }

        float Carve(Vector2 km)
        {
            float deepest = 0f;

            for (int i = 0; i < geography.Rivers.Length; i++)
            {
                if (riverBounds[i].Excludes(km))
                    continue;

                WorldGeography.River river = geography.Rivers[i];
                float distance = rivers[i].Distance(km);
                if (distance >= river.WidthKm)
                    continue;

                deepest = Mathf.Max(deepest,
                    river.CarveMetres * (1f - WorldGeometry.SmoothStep(0f, river.WidthKm, distance)));
            }

            return deepest;
        }

        float NearestRiverKm(Vector2 km)
        {
            float best = float.MaxValue;

            foreach (Course river in rivers)
                best = Mathf.Min(best, river.Distance(km));

            return best;
        }

    }
}
