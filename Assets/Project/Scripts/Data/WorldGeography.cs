using UnityEngine;

namespace EndlessDescent.Data
{
    public enum BiomeId { Sea, Marsh, Fen, Plain, Moor, Dry, Desert, Upland, Mountain }

    // How a biome sits and how rough it is. Heights are metres above sea level, the map records no
    // elevations at all, so these are assigned here and this table is the only place they live
    public readonly struct BiomeProfile
    {
        public readonly float BaseMetres;
        public readonly float ReliefMetres;
        public readonly float NoiseScaleKm;

        // Relief at kilometres is the shape of the map, relief at tens of metres is the only thing
        // you can see standing on it. Without this second band every biome reads as a table
        public readonly float DetailMetres;
        public readonly float DetailScaleKm;

        // Ridge lines at a few kilometres. These are the divides between one settlement's country and
        // the next, they bend a route that would otherwise run straight, and they are what stops you
        // seeing the next town from this one
        public readonly float DivideMetres;
        public readonly float DivideScaleKm;

        // Hills. The other bands are kilometres wide or a hundred metres across, and the gap between
        // them is exactly the scale you read as "country" when you stand in it
        public readonly float HillMetres;
        public readonly float HillScaleKm;

        public BiomeProfile(float baseMetres, float reliefMetres, float noiseScaleKm,
            float detailMetres, float detailScaleKm, float divideMetres, float divideScaleKm,
            float hillMetres, float hillScaleKm)
        {
            BaseMetres = baseMetres;
            ReliefMetres = reliefMetres;
            NoiseScaleKm = noiseScaleKm;
            DetailMetres = detailMetres;
            DetailScaleKm = detailScaleKm;
            DivideMetres = divideMetres;
            DivideScaleKm = divideScaleKm;
            HillMetres = hillMetres;
            HillScaleKm = hillScaleKm;
        }
    }

    // The Ordovan Empire as ground: positions in kilometres, +x east and +y north, the same frame
    // RegionMap places its seventeen locations in. Transcribed from the Imperial Survey map
    public sealed class WorldGeography
    {
        public sealed class Region
        {
            public string Name;
            public BiomeId Biome;
            public Vector2[] Polygon;
            public float BlendKm;
        }

        public sealed class Range
        {
            public string Name;
            public Vector2[] Path;
            public float PeakMetres;
            public float WidthKm;

            // Where the climbing happens. A range whose whole height is spread over WidthKm is a
            // long hill you stroll over, putting the rise into a narrow crest and leaving the rest
            // as an apron of foothills is what makes the great ranges walls and the downs downs.
            // Left at zero it means the full width, which is the gentle shape
            public float CrestKm;

            // Saddles low enough to walk. A wall with no way through it is a wall the roads cannot
            // reach past, and Thessanur is on the far side of one
            public Vector2[] Passes;

            public float Crest => CrestKm > 0f ? CrestKm : WidthKm;
        }

        // Land standing in the ocean. The sea is a half plane north of the coast now, so anything
        // north of it that is not seabed has to say so, and saying so as a shape is what lets the
        // shore wander the way the mainland's does
        public sealed class Island
        {
            public string Name;
            public Vector2[] Polygon;
            public float BlendKm;
        }

        public sealed class River
        {
            public string Name;
            public Vector2[] Path;
            public float CarveMetres;
            public float WidthKm;
        }

        public float CoastNorthKm;

        // How far out the shelf falls away before the floor levels off. The Northern Sea used to be
        // a shallow parabola twenty two kilometres across, which is a river you happen not to have a
        // bridge for. It is open ocean north of the coast now, and the far shore is a shape rather
        // than a latitude, Skarrvald is a coastline that ends, which is what leaves the north west
        // as open water with the Cold Reach standing in it
        public float ShelfKm;
        public float SeaFloorMetres;

        // A coast ruled with a straight edge reads as a border, not a shore. Both lines are pushed
        // north and south by the same slow wave so the strait keeps its width and still wanders,
        // the far shore takes a second, smaller one of its own so the two are not parallel
        public float CoastWarpKm;
        public float ShoreWarpKm;
        public float CoastWarpScaleKm;

        // Region edges get the same treatment. A polygon boundary is a straight cut with mitred
        // corners wherever two of its points meet, and no country is shaped like that
        public float BoundaryWarpKm;
        public float BoundaryWarpScaleKm;

        // The two rivers the Survey drew are transcribed as a handful of points, which walks them
        // dead straight between each pair. Meandering them costs nothing and is the difference
        // between a river and a canal
        public float MeanderKm;
        public float MeanderScaleKm;
        public float MeanderStepKm;
        public Region[] Regions;
        public Island[] Islands;
        public Range[] Ranges;
        public River[] Rivers;
        public Vector2[] Escarpment;
        public float EscarpmentCoreKm;
        public float EscarpmentReachKm;
        public Vector2[] Causeways;

        public static BiomeProfile Profile(BiomeId biome)
        {
            switch (biome)
            {
                case BiomeId.Sea: return new BiomeProfile(0f, 0f, 4f, 0f, 0.2f, 0f, 3f, 0f, 1f);
                case BiomeId.Marsh: return new BiomeProfile(9f, 6f, 3.5f, 12f, 0.11f, 20f, 2.4f, 26f, 1.1f);
                case BiomeId.Fen: return new BiomeProfile(14f, 5f, 3f, 10f, 0.08f, 16f, 2.4f, 20f, 1.1f);
                case BiomeId.Plain: return new BiomeProfile(70f, 24f, 9f, 13f, 0.18f, 30f, 4f, 32f, 1.4f);
                case BiomeId.Moor: return new BiomeProfile(190f, 62f, 7f, 17f, 0.22f, 68f, 3.5f, 70f, 1.3f);
                case BiomeId.Dry: return new BiomeProfile(210f, 52f, 8f, 13f, 0.2f, 52f, 4f, 52f, 1.4f);

                // The Sink is named for what it is, a basin lying below the dry country around it
                case BiomeId.Desert: return new BiomeProfile(90f, 22f, 6f, 11f, 0.14f, 30f, 3f, 34f, 1.2f);

                case BiomeId.Mountain: return new BiomeProfile(130f, 40f, 6f, 30f, 0.3f, 120f, 4f, 90f, 1.6f);

                default: return new BiomeProfile(130f, 48f, 6f, 15f, 0.2f, 82f, 3.5f, 80f, 1.3f);
            }
        }

        // Anything not inside a named region falls back to this, so the surface answers everywhere
        public const BiomeId Default = BiomeId.Upland;

        public static WorldGeography Ordovan() => new WorldGeography
        {
            CoastNorthKm = 82f,
            ShelfKm = 11f,
            SeaFloorMetres = -160f,

            CoastWarpKm = 7f,
            ShoreWarpKm = 4f,
            CoastWarpScaleKm = 34f,

            BoundaryWarpKm = 16f,
            BoundaryWarpScaleKm = 34f,

            MeanderKm = 3.4f,
            MeanderScaleKm = 13f,
            MeanderStepKm = 3f,

            Regions = new[]
            {
                new Region
                {
                    Name = "wet lowland", Biome = BiomeId.Marsh, BlendKm = 17f,
                    Polygon = Pts(-72,78, 92,74, 96,26, 40,-4, -30,0, -70,34)
                },
                new Region
                {
                    Name = "the Tallow Fen", Biome = BiomeId.Fen, BlendKm = 14f,
                    Polygon = Pts(-56,-54, 36,-60, 112,-66, 126,-90, 48,-84, -28,-76, -54,-66)
                },
                new Region
                {
                    Name = "the Casren plain", Biome = BiomeId.Plain, BlendKm = 19f,
                    Polygon = Pts(-38,-72, 118,-84, 124,-198, 36,-216, -34,-190, -46,-118)
                },
                new Region
                {
                    Name = "open moor", Biome = BiomeId.Moor, BlendKm = 16f,
                    Polygon = Pts(-216,32, -72,22, -64,-96, -118,-152, -206,-128, -222,-40)
                },
                new Region
                {
                    Name = "dry country", Biome = BiomeId.Dry, BlendKm = 18f,
                    Polygon = Pts(-56,-232, 76,-238, 104,-282, 24,-292, -40,-278)
                },
                new Region
                {
                    Name = "the Sink", Biome = BiomeId.Desert, BlendKm = 13f,
                    Polygon = Pts(-14,-288, 52,-284, 104,-296, 112,-320, 70,-332, 6,-326, -26,-308)
                }
            },

            // Land in the ocean. Skarrvald is the far shore and runs off the top of the survey the
            // way a border region is supposed to, it is here as a shape only because a coastline
            // that ends is what leaves the north west open, and the Cold Reach standing alone in it
            Islands = new[]
            {
                new Island
                {
                    Name = "Skarrvald", BlendKm = 9f,
                    Polygon = Pts(-58,150, -10,132, 34,110, 84,100, 126,106, 150,124, 150,168, -58,168)
                },
                new Island
                {
                    Name = "the Cold Reach", BlendKm = 8f,
                    Polygon = Pts(-190,126, -166,114, -126,120, -114,140, -140,156, -176,150)
                }
            },

            Ranges = new[]
            {
                new Range
                {
                    Name = "the Thessan Wall", PeakMetres = 1500f, WidthKm = 22f, CrestKm = 1.4f,
                    Path = Pts(138,-214, 144,-150, 140,-88, 146,-30, 134,26, 128,62),

                    // The Ilsemer road crosses at the first, the second is why anyone north of it
                    // has heard of Thessanur at all
                    Passes = Pts(141,-112, 145,-42)
                },
                new Range
                {
                    Name = "the Kerrow", PeakMetres = 1300f, WidthKm = 26f, CrestKm = 1.6f,
                    Path = Pts(-78,-330, -30,-344, 18,-348, 62,-340, 96,-328)
                },
                new Range
                {
                    Name = "the Grey Steps", PeakMetres = 1700f, WidthKm = 24f, CrestKm = 1.5f,
                    Path = Pts(-228,-176, -236,-118, -232,-58, -238,4, -230,52)
                },

                // Smaller high ground, spread so that most of the empire has something on its
                // horizon. The three great ranges all stand on the rim, from Ashmere the nearest is
                // the Thessan Wall, 129 km away and invisible at any draw distance worth having
                new Range { Name = "the Ashfells", PeakMetres = 480f, WidthKm = 10f,
                    Path = Pts(-8,-44, 8,-48, 24,-46) },
                new Range { Name = "the Kelder Rise", PeakMetres = 420f, WidthKm = 9f,
                    Path = Pts(56,44, 64,42, 72,38) },
                new Range { Name = "the Dunmoor Edge", PeakMetres = 380f, WidthKm = 8f,
                    Path = Pts(-46,6, -40,0, -34,-6) },
                new Range { Name = "the Verrin Downs", PeakMetres = 560f, WidthKm = 12f,
                    Path = Pts(110,-120, 116,-112, 122,-104) },
                new Range { Name = "the Oldgate Downs", PeakMetres = 520f, WidthKm = 11f,
                    Path = Pts(-6,-216, 4,-220, 16,-222) },
                new Range { Name = "the Hask Fells", PeakMetres = 640f, WidthKm = 13f,
                    Path = Pts(-158,-52, -152,-48, -146,-44) },
                new Range { Name = "the Colder Fells", PeakMetres = 600f, WidthKm = 12f,
                    Path = Pts(-196,-136, -190,-132, -184,-128) },
                new Range { Name = "the Verrick Downs", PeakMetres = 500f, WidthKm = 11f,
                    Path = Pts(0,-292, 8,-294, 16,-296) }
            },

            Rivers = new[]
            {
                new River
                {
                    Name = "the Casren", CarveMetres = 28f, WidthKm = 2.6f,
                    Path = Pts(136,-118, 104,-134, 68,-146, 36,-142, 16,-110, 4,-62, 6,-16, 12,26, 18,66, 20,82)
                },
                new River
                {
                    Name = "the Verrick", CarveMetres = 22f, WidthKm = 2.2f,
                    Path = Pts(-24,-212, -16,-244, 8,-254, 32,-260, 52,-278, 58,-296, 56,-310)
                }
            },

            // The Dolmarch Edge. It supplies no height of its own: the moor already stands ~120 m
            // over the plain. What it does is force that change to happen in a couple of kilometres
            // instead of across the blend band, which is what makes it an edge and not a slope
            Escarpment = Pts(-60,36, -68,-8, -64,-58, -72,-108, -66,-158, -74,-196),
            EscarpmentCoreKm = 4f,
            EscarpmentReachKm = 10f,

            Causeways = Pts(2,-72, 64,-76, -40,-68)
        };

        // The ground the geography covers, which is also where streams may be seeded
        public Rect Extent()
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            void Take(Vector2[] points)
            {
                foreach (Vector2 p in points)
                {
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }
            }

            foreach (Region region in Regions) Take(region.Polygon);
            foreach (Island island in Islands) Take(island.Polygon);
            foreach (Range range in Ranges) Take(range.Path);
            foreach (River river in Rivers) Take(river.Path);
            Take(Escarpment);

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        static Vector2[] Pts(params float[] xy)
        {
            Vector2[] points = new Vector2[xy.Length / 2];

            for (int i = 0; i < points.Length; i++)
                points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);

            return points;
        }
    }
}
